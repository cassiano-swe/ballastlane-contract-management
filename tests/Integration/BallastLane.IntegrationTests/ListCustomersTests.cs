extern alias business;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class ListCustomersTests
{
    [Fact]
    public async Task ListsAnEmptyTableAndThenAllCustomersInDatabaseOrder()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var dataSource = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(dataSource);
        await using var app = Factory(database.GetConnectionString());
        using var client = app.CreateClient();

        var empty = await ListAsync(client);
        Assert.Equal(JsonValueKind.Array, empty.ValueKind);
        Assert.Equal(0, empty.GetArrayLength());

        using var created = await client.PostAsJsonAsync("/api/customers",
            new { name = "Zulu", customerType = "Individual", taxId = "001'23", email = "a@example.com" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var posted = await created.Content.ReadFromJsonAsync<JsonElement>();
        var single = Assert.Single((await ListAsync(client)).EnumerateArray());
        Assert.Equal(posted.GetProperty("id").GetGuid(), single.GetProperty("id").GetGuid());
        AssertCustomer(single, "Zulu", null, "Individual", "001'23", "a@example.com");

        var firstId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var firstVersion = Guid.NewGuid();
        var secondVersion = Guid.NewGuid();
        await using (var command = dataSource.CreateCommand("""
            INSERT INTO customers (id, name, trade_name, customer_type, tax_id, email, version)
            VALUES (@second, 'Acme', 'West', 2, '003', 'west@example.com', @secondVersion),
                   (@first, 'Acme', 'East', 2, '002', 'east@example.com', @firstVersion)
            """))
        {
            command.Parameters.AddWithValue("first", firstId);
            command.Parameters.AddWithValue("second", secondId);
            command.Parameters.AddWithValue("firstVersion", firstVersion);
            command.Parameters.AddWithValue("secondVersion", secondVersion);
            await command.ExecuteNonQueryAsync();
        }

        var listed = await ListAsync(client);
        Assert.Equal(new[] { firstId, secondId, posted.GetProperty("id").GetGuid() },
            listed.EnumerateArray().Select(customer => customer.GetProperty("id").GetGuid()));
        AssertCustomer(listed[0], "Acme", "East", "Company", "002", "east@example.com");
        AssertCustomer(listed[1], "Acme", "West", "Company", "003", "west@example.com");
        AssertCustomer(listed[2], "Zulu", null, "Individual", "001'23", "a@example.com");
        Assert.Equal(firstVersion, listed[0].GetProperty("version").GetGuid());
        Assert.Equal(secondVersion, listed[1].GetProperty("version").GetGuid());
        Assert.Equal(posted.GetProperty("version").GetGuid(), listed[2].GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task SwaggerDocumentsTheAnonymousCollectionAndItsResponses()
    {
        await using var app = Factory("Host=localhost;Database=unused;Username=unused");
        using var client = app.CreateClient();
        using var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var resource = swagger.RootElement.GetProperty("paths").GetProperty("/api/customers");
        Assert.True(resource.TryGetProperty("post", out _));
        var get = resource.GetProperty("get");
        Assert.Equal("Retrieves all registered customers.", get.GetProperty("summary").GetString());
        Assert.False(get.TryGetProperty("security", out _));
        Assert.False(get.TryGetProperty("requestBody", out _));
        Assert.False(get.TryGetProperty("parameters", out _));
        var responses = get.GetProperty("responses");
        var ok = responses.GetProperty("200");
        Assert.Contains("[]", ok.GetProperty("description").GetString());
        var success = ok.GetProperty("content").GetProperty("application/json");
        Assert.Equal("array", success.GetProperty("schema").GetProperty("type").GetString());
        var example = Assert.Single(success.GetProperty("example").EnumerateArray());
        Assert.NotEqual(Guid.Empty, example.GetProperty("version").GetGuid());
        var schemas = swagger.RootElement.GetProperty("components").GetProperty("schemas");
        var customerProperties = schemas.GetProperty("ListCustomersResponse").GetProperty("properties");
        Assert.True(customerProperties.GetProperty("tradeName").GetProperty("nullable").GetBoolean());
        Assert.Equal("uuid", customerProperties.GetProperty("version").GetProperty("format").GetString());
        var customerType = schemas.GetProperty("CustomerType");
        Assert.Equal("string", customerType.GetProperty("type").GetString());
        Assert.Equal(new[] { "Individual", "Company" },
            customerType.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        var problem = responses.GetProperty("500").GetProperty("content").GetProperty("application/problem+json");
        Assert.Equal("#/components/schemas/ProblemDetails", problem.GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task UnexpectedFailureUsesCentralizedProblemDetailsAndRequestLogging()
    {
        using var logs = new TestLogProvider();
        await using var app = Factory("Host=localhost;Database=unused;Username=unused").WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICustomerRepository>();
                services.AddScoped<ICustomerRepository, FailingRepository>();
            });
        });
        using var client = app.CreateClient();
        using var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var detail = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test-sensitive-detail", detail);
        using var problem = JsonDocument.Parse(detail);
        var trace = problem.RootElement.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(trace));
        Assert.Contains(logs.Entries, values =>
            Equals(values.GetValueOrDefault("Method"), "GET")
            && Equals(values.GetValueOrDefault("Route"), "api/customers")
            && Equals(values.GetValueOrDefault("StatusCode"), 500)
            && Equals(values.GetValueOrDefault("TraceIdentifier"), trace)
            && values.ContainsKey("ElapsedMilliseconds"));
    }

    private static WebApplicationFactory<business::Program> Factory(string connectionString) =>
        new WebApplicationFactory<business::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = connectionString,
                    ["Jwt:Issuer"] = "",
                    ["Jwt:Audience"] = "",
                    ["Jwt:SigningKey"] = ""
                }));
        });

    private static async Task<JsonElement> ListAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertCustomer(JsonElement customer, string name, string? tradeName, string type, string taxId, string email)
    {
        Assert.Equal(7, customer.EnumerateObject().Count());
        Assert.Equal(name, customer.GetProperty("name").GetString());
        Assert.Equal(tradeName, customer.GetProperty("tradeName").GetString());
        Assert.Equal(type, customer.GetProperty("customerType").GetString());
        Assert.Equal(taxId, customer.GetProperty("taxId").GetString());
        Assert.Equal(email, customer.GetProperty("email").GetString());
    }

    private sealed class FailingRepository : ICustomerRepository
    {
        public Task CreateAsync(Customer customer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("test-sensitive-detail");

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
