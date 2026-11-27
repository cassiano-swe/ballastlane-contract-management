extern alias business;
extern alias identity;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BallastLane.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public sealed class AuthenticationTests
{
    private const string DemoEmail = "demo@ballastlane.example";
    private const string DemoPassword = "BallastLane@2026!";
    private const string LoginPath = "/api/auth/login";
    private const string CustomerPath = "/api/customers";

    [Fact]
    public async Task DemoLoginAuthorizesCustomerLifecycleAcrossHosts()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        var repository = new UserRepository(source);
        var seed = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "seeds", "001_create_demo_user.sql"));
        await using var seedCommand = source.CreateCommand(seed);
        await seedCommand.ExecuteNonQueryAsync();
        var before = await repository.GetByEmailAsync(DemoEmail, default);
        Assert.NotNull(before);
        await seedCommand.ExecuteNonQueryAsync();
        var after = await repository.GetByEmailAsync(DemoEmail, default);
        Assert.NotNull(after);
        Assert.Equal((before.Id, before.Name, before.Email, before.Active), (after.Id, after.Name, after.Email, after.Active));
        Assert.True(before.PasswordHash == after.PasswordHash);
        await using var count = source.CreateCommand("SELECT count(*) FROM users WHERE email = 'demo@ballastlane.example'");
        Assert.Equal(1L, await count.ExecuteScalarAsync());

        using var logs = new TestLogProvider();
        await using var identityApp = Factory<identity::Program>(database.GetConnectionString(), logs);
        await using var businessApp = Factory<business::Program>(database.GetConnectionString(), logs);
        using var identityClient = identityApp.CreateClient();
        using var businessClient = businessApp.CreateClient();
        using var login = await identityClient.PostAsJsonAsync(LoginPath, new { email = " DEMO@BALLASTLANE.EXAMPLE ", password = DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.CacheControl?.NoStore);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "accessToken", "expiresIn", "tokenType" }, loginBody.EnumerateObject().Select(field => field.Name).Order());
        Assert.Equal("Bearer", loginBody.GetProperty("tokenType").GetString());
        Assert.Equal(1800, loginBody.GetProperty("expiresIn").GetInt32());
        var token = loginBody.GetProperty("accessToken").GetString()!;
        Assert.Equal(before.Id.ToString(), new JsonWebTokenHandler().ReadJsonWebToken(token).Subject);
        businessClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (path, created) = await UpdateCustomerTests.CreateAsync(businessClient, "AUTH-TEST-001");
        var originalVersion = UpdateCustomerTests.Version(created);
        CustomerJsonAssertions.MatchesWriteResponse(created, await UpdateCustomerTests.ReadAsync(businessClient, path), active: true);
        Assert.Single((await UpdateCustomerTests.ReadAsync(businessClient, CustomerPath)).EnumerateArray());

        using var duplicate = await businessClient.PostAsJsonAsync(CustomerPath,
            new { name = "Duplicate", customerType = "Individual", taxId = "AUTH-TEST-001", email = "dup@example.com" });
        await AssertConflict(duplicate, "urn:ballastlane:customer-duplicate-tax-id");
        using var update = await businessClient.PutAsJsonAsync(path,
            UpdateCustomerTests.Body("Updated", "AUTH-TEST-001", originalVersion));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updatedVersion = UpdateCustomerTests.Version(await update.Content.ReadFromJsonAsync<JsonElement>());
        Assert.NotEqual(originalVersion, updatedVersion);
        using var stale = await businessClient.DeleteAsync($"{path}?version={originalVersion}");
        await AssertConflict(stale, "urn:ballastlane:customer-concurrency-conflict");
        using var deleted = await businessClient.DeleteAsync($"{path}?version={updatedVersion}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var inactive = await UpdateCustomerTests.ReadAsync(businessClient, path);
        Assert.False(inactive.GetProperty("active").GetBoolean());
        Assert.NotEqual(updatedVersion, UpdateCustomerTests.Version(inactive));

        var failures = new[]
        {
            await AuthenticationFailure(identityClient, DemoEmail, "incorrect"),
            await AuthenticationFailure(identityClient, "missing@example.com", DemoPassword)
        };
        await using var deactivate = source.CreateCommand("""
            UPDATE users SET name = 'Test account', active = FALSE WHERE email = 'demo@ballastlane.example';
            """);
        await deactivate.ExecuteNonQueryAsync();
        await seedCommand.ExecuteNonQueryAsync();
        var preserved = await repository.GetByEmailAsync(DemoEmail, default);
        Assert.NotNull(preserved);
        Assert.False(preserved.Active);
        Assert.Equal("Test account", preserved.Name);
        Assert.Equal(before.Id, preserved.Id);
        Assert.True(before.PasswordHash == preserved.PasswordHash);
        var inactiveFailure = await AuthenticationFailure(identityClient, DemoEmail, DemoPassword);
        Assert.All(failures, failure => Assert.Equal(inactiveFailure, failure));
        using var invalid = await identityClient.PostAsJsonAsync(LoginPath, new { email = "invalid-sensitive-email", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.DoesNotContain("invalid-sensitive-email", await invalid.Content.ReadAsStringAsync());

        var logged = string.Join("\n", logs.Entries.SelectMany(entry => entry.Values));
        Assert.False(logged.Contains(DemoPassword, StringComparison.Ordinal));
        Assert.False(logged.Contains(token, StringComparison.Ordinal));
        Assert.False(logged.Contains(before.PasswordHash, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("GET", "")]
    [InlineData("GET", "/4a23620c-6994-4d67-80cc-9b347568f585")]
    [InlineData("PUT", "/4a23620c-6994-4d67-80cc-9b347568f585")]
    [InlineData("DELETE", "/4a23620c-6994-4d67-80cc-9b347568f585?version=4a23620c-6994-4d67-80cc-9b347568f585")]
    public async Task AllCustomerOperationsRequireBearerCredentials(string method, string suffix)
    {
        await using var app = Factory<business::Program>();
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), CustomerPath + suffix);
        using var response = await client.SendAsync(request);
        await AssertUnauthorized(response);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("tampered")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("unsigned")]
    [InlineData("algorithm")]
    public async Task RejectsInvalidTokens(string scenario)
    {
        await using var app = Factory<business::Program>();
        using var client = app.CreateClient();
        var randomKey = scenario == "signature" ? RandomNumberGenerator.GetBytes(32) : Convert.FromBase64String(TestJwt.SigningKey);
        var algorithm = SecurityAlgorithms.HmacSha256;
        if (scenario == "algorithm")
            algorithm = SecurityAlgorithms.HmacSha256Signature;
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = scenario == "issuer" ? "other-issuer" : TestJwt.Issuer,
            Audience = scenario == "audience" ? "other-audience" : TestJwt.Audience,
            IssuedAt = now.AddMinutes(-5),
            NotBefore = now.AddMinutes(-5),
            Expires = scenario == "expired" ? now.AddMinutes(-2) : now.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = TestJwt.UserId.ToString() },
            SigningCredentials = scenario == "unsigned" ? null : new SigningCredentials(new SymmetricSecurityKey(randomKey), algorithm)
        };
        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        if (scenario == "tampered")
        {
            var parts = token.Split('.');
            parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
            token = string.Join(".", parts);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(CustomerPath);
        await AssertUnauthorized(response);
    }

    [Fact]
    public async Task SwaggerAndHealthExposeOnlyIntendedAnonymousOperations()
    {
        await using var identityApp = Factory<identity::Program>();
        await using var webApp = Factory<business::Program>();
        using var identityClient = identityApp.CreateClient();
        using var webClient = webApp.CreateClient();
        foreach (var client in new[] { identityClient, webClient })
        {
            using var health = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
            var bearer = swagger.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
            Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
            var paths = swagger.GetProperty("paths");
            Assert.False(paths.GetProperty("/health").GetProperty("get").TryGetProperty("security", out _));
            if (ReferenceEquals(client, identityClient))
            {
                var login = paths.GetProperty(LoginPath).GetProperty("post");
                Assert.False(login.TryGetProperty("security", out _));
                Assert.Equal(new[] { "200", "400", "401", "500" }, login.GetProperty("responses").EnumerateObject().Select(x => x.Name).Order());
            }
            else
            {
                var customerOperations = paths.GetProperty(CustomerPath).EnumerateObject().Concat(
                    paths.GetProperty(CustomerPath + "/{id}").EnumerateObject()).ToArray();
                Assert.Equal(5, customerOperations.Length);
                Assert.All(customerOperations, operation =>
                    Assert.True(Assert.Single(operation.Value.GetProperty("security").EnumerateArray()).TryGetProperty("Bearer", out _)));
            }
        }
    }

    private static WebApplicationFactory<T> Factory<T>(string? connection = null, ILoggerProvider? logs = null) where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestJwt.Issuer, ["Jwt:Audience"] = TestJwt.Audience, ["Jwt:SigningKey"] = TestJwt.SigningKey,
                ["Jwt:AccessTokenLifetimeSeconds"] = "1800",
                ["ConnectionStrings:Postgres"] = connection ?? "Host=localhost;Database=unused;Username=unused"
            }));
            if (logs is not null)
                builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });

    private static async Task<(string?, string?, string?)> AuthenticationFailure(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync(LoginPath, new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(problem.TryGetProperty("accessToken", out _));
        Assert.False(problem.GetRawText().Contains(password, StringComparison.Ordinal));
        Assert.False(problem.GetRawText().Contains(email, StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        return (problem.GetProperty("type").GetString(), problem.GetProperty("title").GetString(), problem.GetProperty("detail").GetString());
    }

    private static async Task AssertUnauthorized(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Null(challenge.Parameter);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
    }

    private static async Task AssertConflict(HttpResponseMessage response, string type)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(type, body.GetProperty("type").GetString());
    }
}
