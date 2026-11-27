extern alias identity;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BallastLane.Domain.Entities;
using BallastLane.Infrastructure.Persistence.Repositories;
using BallastLane.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public sealed class CustomerAuthorshipTests
{
    private const string Customers = "/api/customers";
    private const string TestPassword = "authorship-test-password";

    [Fact]
    public async Task PersistsAuthenticatedAuthorsAndKeepsLegacyAndRejectedWritesIntact()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        var connection = database.GetConnectionString();
        await using var source = NpgsqlDataSource.Create(connection);
        foreach (var migration in new[] { "001_create_customers.sql", "002_add_customer_version.sql", "003_add_customer_active.sql", "004_create_users.sql" })
            await CustomerMigrations.ApplyAsync(source, migration);
        await using var legacy = source.CreateCommand("""
            INSERT INTO customers (id, name, customer_type, tax_id, email, version)
            VALUES (gen_random_uuid(), 'Legacy', 1, 'legacy', 'legacy@example.test', gen_random_uuid())
            RETURNING id;
            """);
        var legacyId = Assert.IsType<Guid>(await legacy.ExecuteScalarAsync());
        await CustomerMigrations.ApplyAsync(source, "005_add_customer_authorship.sql");
        var legacyBefore = await ReadRowAsync(source, legacyId);
        AssertAuthor(legacyBefore, "created_by_user_id", null);
        AssertAuthor(legacyBefore, "updated_by_user_id", null);

        var users = new UserRepository(source);
        var hasher = new UserPasswordHasher();
        var first = new User("First", "first@example.test", hasher.Hash(TestPassword));
        var second = new User("Second", "second@example.test", hasher.Hash(TestPassword));
        await users.CreateAsync(first, default);
        await users.CreateAsync(second, default);
        await using var identity = new WebApplicationFactory<identity::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestJwt.Issuer, ["Jwt:Audience"] = TestJwt.Audience, ["Jwt:SigningKey"] = TestJwt.SigningKey,
                ["ConnectionStrings:Postgres"] = connection
            }));
        });
        await using var business = UpdateCustomerTests.Factory(connection);
        using var login = identity.CreateClient();
        using var asFirst = business.CreateClient();
        using var asSecond = business.CreateClient();
        asFirst.DefaultRequestHeaders.Authorization = await LoginAsync(login, first.Email);
        asSecond.DefaultRequestHeaders.Authorization = await LoginAsync(login, second.Email);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"{Customers}?createdByUserId={second.Id}")
        {
            Content = JsonContent.Create(new
            {
                name = "New", customerType = "Individual", taxId = "audited", email = "new@example.test",
                createdByUserId = second.Id, updatedByUserId = second.Id
            })
        };
        createRequest.Headers.Add("X-User-Id", second.Id.ToString());
        using var create = await asFirst.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var path = $"{Customers}/{id}";
        var originalVersion = UpdateCustomerTests.Version(created);
        var inserted = await ReadRowAsync(source, id);
        AssertAuthor(inserted, "created_by_user_id", first.Id);
        AssertAuthor(inserted, "updated_by_user_id", null);

        var replacement = UpdateCustomerTests.Body("Edited by second", "audited", originalVersion);
        replacement["createdByUserId"] = second.Id;
        replacement["updatedByUserId"] = first.Id;
        using var edit = await asSecond.PutAsJsonAsync(path, replacement);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<JsonElement>();
        var editedVersion = UpdateCustomerTests.Version(edited);
        Assert.NotEqual(originalVersion, editedVersion);
        var editedRow = await ReadRowAsync(source, id);
        AssertAuthor(editedRow, "created_by_user_id", first.Id);
        AssertAuthor(editedRow, "updated_by_user_id", second.Id);
        Assert.Equal("Edited by second", editedRow.GetProperty("name").GetString());

        using var stale = await asFirst.PutAsJsonAsync(path, UpdateCustomerTests.Body("Stale edit", "audited", originalVersion));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.True(JsonElement.DeepEquals(editedRow, await ReadRowAsync(source, id)));

        using var invalidClient = business.CreateClient();
        foreach (var authorization in new AuthenticationHeaderValue?[]
        {
            null, CreateIdentityWithoutValidSubject(null), CreateIdentityWithoutValidSubject("invalid"),
            CreateIdentityWithoutValidSubject(Guid.Empty.ToString())
        })
        {
            invalidClient.DefaultRequestHeaders.Authorization = authorization;
            using var rejectedCreate = await invalidClient.PostAsJsonAsync(Customers,
                new { name = "Rejected", customerType = "Individual", taxId = "rejected", email = "bad@example.test" });
            using var rejectedEdit = await invalidClient.PutAsJsonAsync(path,
                UpdateCustomerTests.Body("Rejected", "audited", editedVersion));
            using var rejectedDelete = await invalidClient.DeleteAsync($"{path}?version={editedVersion}");
            foreach (var rejected in new[] { rejectedCreate, rejectedEdit, rejectedDelete })
            {
                Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
                Assert.Equal("Bearer", Assert.Single(rejected.Headers.WwwAuthenticate).Scheme);
            }
        }
        await using var count = source.CreateCommand("SELECT count(*) FROM customers");
        Assert.Equal(2L, await count.ExecuteScalarAsync());
        Assert.True(JsonElement.DeepEquals(editedRow, await ReadRowAsync(source, id)));

        using var delete = await asFirst.DeleteAsync($"{path}?version={editedVersion}&updatedByUserId={second.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var deactivated = await ReadRowAsync(source, id);
        Assert.False(deactivated.GetProperty("active").GetBoolean());
        AssertAuthor(deactivated, "created_by_user_id", first.Id);
        AssertAuthor(deactivated, "updated_by_user_id", first.Id);
        var inactiveVersion = deactivated.GetProperty("version").GetGuid();
        Assert.NotEqual(editedVersion, inactiveVersion);
        using var repeated = await asSecond.DeleteAsync($"{path}?version={inactiveVersion}");
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.True(JsonElement.DeepEquals(deactivated, await ReadRowAsync(source, id)));
        var read = await UpdateCustomerTests.ReadAsync(asSecond, path);
        Assert.False(read.TryGetProperty("createdByUserId", out _));
        Assert.False(read.TryGetProperty("updatedByUserId", out _));

        using var legacyEdit = await asSecond.PutAsJsonAsync($"{Customers}/{legacyId}",
            UpdateCustomerTests.Body("Edited legacy", "legacy", legacyBefore.GetProperty("version").GetGuid()));
        Assert.Equal(HttpStatusCode.OK, legacyEdit.StatusCode);
        var legacyAfter = await ReadRowAsync(source, legacyId);
        AssertAuthor(legacyAfter, "created_by_user_id", null);
        AssertAuthor(legacyAfter, "updated_by_user_id", second.Id);

        await using var removeAuthor = source.CreateCommand("DELETE FROM users WHERE id = @id");
        removeAuthor.Parameters.AddWithValue("id", first.Id);
        var foreignKey = await Assert.ThrowsAsync<PostgresException>(() => removeAuthor.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreignKey.SqlState);
    }

    private static async Task<AuthenticationHeaderValue> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private static AuthenticationHeaderValue CreateIdentityWithoutValidSubject(string? subject)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = TestJwt.Issuer, Audience = TestJwt.Audience, IssuedAt = now, Expires = now.AddMinutes(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(TestJwt.SigningKey)), SecurityAlgorithms.HmacSha256),
            Claims = subject is null ? null : new Dictionary<string, object> { ["sub"] = subject }
        };
        return new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(descriptor));
    }

    private static async Task<JsonElement> ReadRowAsync(NpgsqlDataSource source, Guid id)
    {
        await using var command = source.CreateCommand("SELECT to_jsonb(customers)::text FROM customers WHERE id = @id");
        command.Parameters.AddWithValue("id", id);
        using var row = JsonDocument.Parse(Assert.IsType<string>(await command.ExecuteScalarAsync()));
        return row.RootElement.Clone();
    }

    private static void AssertAuthor(JsonElement row, string column, Guid? expected)
    {
        var value = row.GetProperty(column);
        Assert.Equal(expected, value.ValueKind == JsonValueKind.Null ? (Guid?)null : value.GetGuid());
    }
}
