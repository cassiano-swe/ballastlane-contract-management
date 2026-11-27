extern alias business;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.Infrastructure.Persistence.Repositories;
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

public class GetCustomerByIdTests
{
    [Fact]
    public async Task RetrievesTheRequestedRowAndFollowsThePostLocation()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        var connectionString = database.GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "001_create_customers.sql"));
        await using (var command = dataSource.CreateCommand(migration))
            await command.ExecuteNonQueryAsync();

        var id = Guid.NewGuid();
        var repository = new CustomerRepository(dataSource);
        await repository.CreateAsync(Customer.Reconstitute(id, "Alex Taylor", null, CustomerType.Individual,
            "001'23", "alex@example.com"), default);
        using var logs = new TestLogProvider();
        await using var app = Factory(connectionString).WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = app.CreateClient();

        using var created = await client.PostAsJsonAsync("/api/customers",
            new { name = "Acme", tradeName = "Shop", customerType = "Company", taxId = "002", email = "a@example.com" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var posted = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"/api/customers/{posted.GetProperty("id").GetGuid()}", created.Headers.Location?.OriginalString);
        using var fetched = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.True(JsonElement.DeepEquals(posted, await fetched.Content.ReadFromJsonAsync<JsonElement>()));

        using var existing = await client.GetAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        var body = await existing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.Equal(6, body.EnumerateObject().Count());
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Alex Taylor", body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("tradeName").ValueKind);
        Assert.Equal("Individual", body.GetProperty("customerType").GetString());
        Assert.Equal("001'23", body.GetProperty("taxId").GetString());
        Assert.Equal("alex@example.com", body.GetProperty("email").GetString());

        var missingId = Guid.NewGuid();
        using var missing = await client.GetAsync($"/api/customers/{missingId}");
        var problem = await ReadProblem(missing, HttpStatusCode.NotFound, $"/api/customers/{missingId}");
        Assert.Equal("Customer not found", problem.GetProperty("title").GetString());
        Assert.Equal("The requested customer was not found.", problem.GetProperty("detail").GetString());
        AssertRequestLog(logs, 404, problem.GetProperty("traceId").GetString());
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement>("/api/customers")).GetArrayLength());
        await AssertRepositoryLookup(repository, id, missingId);
    }

    [Theory]
    [InlineData("malformed-id", "The customer identifier must be a valid UUID.")]
    [InlineData("00000000-0000-0000-0000-000000000000", "The customer identifier must not be empty.")]
    public async Task InvalidIdReturnsTraceableValidationProblem(string id, string message)
    {
        await using var app = Factory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<ICustomerRepository>()
                .AddScoped<ICustomerRepository, FailingRepository>()));
        using var client = app.CreateClient();
        using var response = await client.GetAsync($"/api/customers/{id}");
        var problem = await ReadProblem(response, HttpStatusCode.BadRequest, $"/api/customers/{id}");
        Assert.Equal(message, problem.GetProperty("detail").GetString());
        var field = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal("id", field.Name, ignoreCase: true);
        Assert.Equal(message, Assert.Single(field.Value.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task UnexpectedFailureReturnsSafeProblemAndLogsMatchingTrace()
    {
        using var logs = new TestLogProvider();
        await using var app = Factory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services => services.RemoveAll<ICustomerRepository>()
                .AddScoped<ICustomerRepository, FailingRepository>());
        });
        using var client = app.CreateClient();
        var path = $"/api/customers/{Guid.NewGuid()}";
        using var response = await client.GetAsync(path);
        var problem = await ReadProblem(response, HttpStatusCode.InternalServerError, path);
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("detail").GetString());
        Assert.DoesNotContain("test-sensitive-detail", problem.GetRawText());
        AssertRequestLog(logs, 500, problem.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task SwaggerDocumentsTheAnonymousByIdOperation()
    {
        await using var app = Factory();
        using var client = app.CreateClient();
        using var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = swagger.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/customers").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/customers").TryGetProperty("get", out _));
        var operation = paths.GetProperty("/api/customers/{id}").GetProperty("get");
        Assert.Equal("Retrieves a customer by its unique identifier.", operation.GetProperty("summary").GetString());
        Assert.Contains("Anonymous access is temporary", operation.GetProperty("description").GetString());
        Assert.False(operation.TryGetProperty("security", out _));
        Assert.False(operation.TryGetProperty("requestBody", out _));
        var parameter = Assert.Single(operation.GetProperty("parameters").EnumerateArray());
        Assert.Equal("id", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
        Assert.True(parameter.GetProperty("required").GetBoolean());
        Assert.Equal("uuid", parameter.GetProperty("schema").GetProperty("format").GetString());
        var responses = operation.GetProperty("responses");
        var success = responses.GetProperty("200").GetProperty("content").GetProperty("application/json");
        Assert.Equal("#/components/schemas/GetCustomerByIdResponse", success.GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal(JsonValueKind.Object, success.GetProperty("example").ValueKind);
        foreach (var (status, schema) in new[] { ("400", "ValidationProblemDetails"), ("404", "ProblemDetails"), ("500", "ProblemDetails") })
        {
            var mediaType = responses.GetProperty(status).GetProperty("content").GetProperty("application/problem+json");
            Assert.Equal($"#/components/schemas/{schema}", mediaType.GetProperty("schema").GetProperty("$ref").GetString());
        }
        var schemas = swagger.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("GetCustomerByIdResponse").GetProperty("properties")
            .GetProperty("tradeName").GetProperty("nullable").GetBoolean());
        var customerType = schemas.GetProperty("CustomerType");
        Assert.Equal("string", customerType.GetProperty("type").GetString());
        Assert.Equal(new[] { "Individual", "Company" },
            customerType.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }

    private static WebApplicationFactory<business::Program> Factory(string connectionString = "Host=localhost;Database=unused;Username=unused") =>
        new WebApplicationFactory<business::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["Jwt:Issuer"] = "",
                ["Jwt:Audience"] = "",
                ["Jwt:SigningKey"] = ""
            }));
        });

    private static async Task<JsonElement> ReadProblem(HttpResponseMessage response, HttpStatusCode status, string path)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(path, problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    private static void AssertRequestLog(TestLogProvider logs, int status, string? trace)
    {
        Assert.Contains(logs.Entries, values =>
            Equals(values.GetValueOrDefault("Method"), "GET")
            && Equals(values.GetValueOrDefault("Route"), "api/customers/{id}")
            && Equals(values.GetValueOrDefault("StatusCode"), status)
            && Equals(values.GetValueOrDefault("TraceIdentifier"), trace)
            && values.ContainsKey("ElapsedMilliseconds"));
    }

    private static async Task AssertRepositoryLookup(CustomerRepository repository, Guid id, Guid missingId)
    {
        var customer = Assert.IsType<Customer>(await repository.GetByIdAsync(id, default));
        Assert.Equal((id, "Alex Taylor", (string?)null, CustomerType.Individual, "001'23", "alex@example.com"),
            (customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email));
        Assert.Null(await repository.GetByIdAsync(missingId, default));
    }

    private sealed class FailingRepository : ICustomerRepository
    {
        public Task CreateAsync(Customer customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("test-sensitive-detail");
    }
}
