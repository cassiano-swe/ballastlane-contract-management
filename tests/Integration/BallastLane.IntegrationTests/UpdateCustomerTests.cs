extern alias business;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Domain.Entities;
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

public class UpdateCustomerTests
{
    [Fact]
    public async Task ReplacesStateRejectsStaleChangesAndAllowsNewAttemptWithLatestVersion()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        using var logs = new TestLogProvider();
        await using var factory = Factory(database.GetConnectionString(), configure: builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateAuthenticatedClient();
        var (path, created) = await CreateAsync(client, "001");
        await CreateAsync(client, "002");
        var initial = await ReadAsync(client, path);
        CustomerJsonAssertions.MatchesWriteResponse(created, initial, active: true);
        var originalVersion = Version(initial);

        var replacement = Body(" Updated ", "001'23", originalVersion);
        replacement["active"] = false;
        using var first = await client.PutAsJsonAsync(path, replacement);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var updated = await first.Content.ReadFromJsonAsync<JsonElement>();
        var newVersion = Version(updated);
        Assert.NotEqual(Guid.Empty, newVersion);
        Assert.NotEqual(originalVersion, newVersion);
        Assert.Equal(7, updated.EnumerateObject().Count());
        Assert.Equal(initial.GetProperty("id").GetGuid(), updated.GetProperty("id").GetGuid());
        Assert.Equal("Updated", updated.GetProperty("name").GetString());
        Assert.Equal("Individual", updated.GetProperty("customerType").GetString());
        Assert.Equal("001'23", updated.GetProperty("taxId").GetString());
        Assert.Equal("update@example.com", updated.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("tradeName").ValueKind);

        using var stale = await client.PutAsJsonAsync(path, Body("Stale", "003", originalVersion));
        var staleProblem = await ProblemAsync(stale, HttpStatusCode.Conflict, path);
        Assert.Equal("urn:ballastlane:customer-concurrency-conflict", staleProblem.GetProperty("type").GetString());
        Assert.Contains(logs.Entries, entry => Equals(entry.GetValueOrDefault("StatusCode"), 409)
            && Equals(entry.GetValueOrDefault("TraceIdentifier"), staleProblem.GetProperty("traceId").GetString())
            && Equals(entry.GetValueOrDefault("Route"), "api/customers/{id}"));

        using var duplicate = await client.PutAsJsonAsync(path, Body("Duplicate", "002", newVersion));
        var duplicateProblem = await ProblemAsync(duplicate, HttpStatusCode.Conflict, path);
        Assert.Equal("urn:ballastlane:customer-duplicate-tax-id", duplicateProblem.GetProperty("type").GetString());
        CustomerJsonAssertions.MatchesWriteResponse(updated, await ReadAsync(client, path), active: true);

        using var repeat = await client.PutAsJsonAsync(path, Body("Updated", "001'23", newVersion));
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var latest = await repeat.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(newVersion, Version(latest));
        var current = await ReadAsync(client, path);
        CustomerJsonAssertions.MatchesWriteResponse(latest, current, active: true);
        var collection = await ReadAsync(client, "/api/customers");
        Assert.Contains(collection.EnumerateArray(), item => JsonElement.DeepEquals(item, current));
        var persisted = Assert.IsType<Customer>(await new CustomerRepository(source).GetByIdAsync(latest.GetProperty("id").GetGuid(), default));
        Assert.Equal(Version(latest), persisted.Version);

        var missingPath = $"/api/customers/{Guid.NewGuid()}";
        using var missing = await client.PutAsJsonAsync(missingPath, Body("Missing", "003", originalVersion));
        await ProblemAsync(missing, HttpStatusCode.NotFound, missingPath);
    }

    [Fact]
    public async Task BothRequestsReadSameVersionButExactlyOnePersists()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        var repository = new RacingRepository(new CustomerRepository(source));
        await using var factory = Factory(database.GetConnectionString(), repository);
        using var client = factory.CreateAuthenticatedClient();
        var (path, created) = await CreateAsync(client, "001");
        var version = Version(created);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        repository.Arm();

        var responses = await Task.WhenAll(
            client.PutAsJsonAsync(path, Body("First", "002", version), timeout.Token),
            client.PutAsJsonAsync(path, Body("Second", "003", version), timeout.Token));
        using var one = responses[0];
        using var two = responses[1];
        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var problem = await ProblemAsync(conflict, HttpStatusCode.Conflict, path);
        Assert.Equal("urn:ballastlane:customer-concurrency-conflict", problem.GetProperty("type").GetString());
        Assert.Equal(new[] { version, version }, repository.ObservedVersions);
        Assert.Equal(2, repository.WriteAttempts);
        var winner = await success.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(version, Version(winner));
        Assert.Equal(ReferenceEquals(success, one) ? "First" : "Second", winner.GetProperty("name").GetString());
        CustomerJsonAssertions.MatchesWriteResponse(winner, await ReadAsync(client, path), active: true);
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("empty-id")]
    [InlineData("missing-version")]
    [InlineData("null-version")]
    [InlineData("malformed-version")]
    [InlineData("empty-version")]
    [InlineData("undefined-type")]
    [InlineData("numeric-type")]
    [InlineData("numeric-string-type")]
    [InlineData("missing-type")]
    [InlineData("invalid-name")]
    public async Task InvalidInputReturnsTraceableValidationProblem(string scenario)
    {
        await using var factory = Factory(repository: new FailingRepository());
        using var client = factory.CreateAuthenticatedClient();
        var id = scenario switch
        {
            "invalid-id" => "malformed-id",
            "empty-id" => Guid.Empty.ToString(),
            _ => Guid.NewGuid().ToString()
        };
        var body = Body("New", "001", Guid.NewGuid());
        switch (scenario)
        {
            case "missing-version": body.Remove("version"); break;
            case "null-version": body["version"] = null; break;
            case "malformed-version": body["version"] = "invalid-version-value"; break;
            case "empty-version": body["version"] = Guid.Empty; break;
            case "undefined-type": body["customerType"] = "Undefined"; break;
            case "numeric-type": body["customerType"] = 2; break;
            case "numeric-string-type": body["customerType"] = "2"; break;
            case "missing-type": body.Remove("customerType"); break;
            case "invalid-name": body["name"] = new string('n', 201); break;
        }
        var path = $"/api/customers/{id}";
        using var response = await client.PutAsJsonAsync(path, body);
        var problem = await ProblemAsync(response, HttpStatusCode.BadRequest, path);
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
        Assert.DoesNotContain("invalid-version-value", problem.GetRawText());
    }

    [Fact]
    public async Task UnexpectedFailureIsSafeAndSwaggerDescribesProtectedPut()
    {
        using var logs = new TestLogProvider();
        await using var factory = Factory(repository: new FailingRepository(), configure: builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateAuthenticatedClient();
        var path = $"/api/customers/{Guid.NewGuid()}";
        using var response = await client.PutAsJsonAsync(path, Body("New", "001", Guid.NewGuid()));
        var problem = await ProblemAsync(response, HttpStatusCode.InternalServerError, path);
        Assert.DoesNotContain("sensitive-repository-detail", problem.GetRawText());
        Assert.Contains(logs.Entries, entry => Equals(entry.GetValueOrDefault("StatusCode"), 500)
            && Equals(entry.GetValueOrDefault("TraceIdentifier"), problem.GetProperty("traceId").GetString()));

        using var swaggerUi = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, swaggerUi.StatusCode);
        var swagger = await ReadAsync(client, "/swagger/v1/swagger.json");
        var operation = swagger.GetProperty("paths").GetProperty("/api/customers/{id}").GetProperty("put");
        Assert.Equal("Updates an existing customer using optimistic concurrency control.", operation.GetProperty("summary").GetString());
        Assert.True(Assert.Single(operation.GetProperty("security").EnumerateArray()).TryGetProperty("Bearer", out _));
        var id = Assert.Single(operation.GetProperty("parameters").EnumerateArray());
        Assert.True(id.GetProperty("required").GetBoolean());
        Assert.Equal("uuid", id.GetProperty("schema").GetProperty("format").GetString());
        var responses = operation.GetProperty("responses");
        Assert.Equal(new[] { "200", "400", "401", "404", "409", "500" }, responses.EnumerateObject().Select(item => item.Name).Order().ToArray());
        var examples = responses.GetProperty("409").GetProperty("content").GetProperty("application/problem+json").GetProperty("examples");
        Assert.Equal("urn:ballastlane:customer-concurrency-conflict", examples.GetProperty("concurrency").GetProperty("value").GetProperty("type").GetString());
        Assert.Equal("urn:ballastlane:customer-duplicate-tax-id", examples.GetProperty("duplicateTaxId").GetProperty("value").GetProperty("type").GetString());
        var schemas = swagger.GetProperty("components").GetProperty("schemas");
        var request = schemas.GetProperty("UpdateCustomerRequest");
        Assert.Equal(new[] { "customerType", "email", "name", "taxId", "version" },
            request.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Order().ToArray());
        var fields = request.GetProperty("properties");
        Assert.False(fields.TryGetProperty("id", out _));
        Assert.True(fields.GetProperty("tradeName").GetProperty("nullable").GetBoolean());
        Assert.Equal(200, fields.GetProperty("name").GetProperty("maxLength").GetInt32());
        Assert.Equal(200, fields.GetProperty("tradeName").GetProperty("maxLength").GetInt32());
        Assert.Equal(50, fields.GetProperty("taxId").GetProperty("maxLength").GetInt32());
        Assert.Equal(254, fields.GetProperty("email").GetProperty("maxLength").GetInt32());
        var version = fields.GetProperty("version");
        Assert.Equal("uuid", version.GetProperty("format").GetString());
        Assert.False(version.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean());
        Assert.Equal(new[] { "Individual", "Company" }, schemas.GetProperty("CustomerType").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.Equal("uuid", schemas.GetProperty("UpdateCustomerResponse").GetProperty("properties").GetProperty("version").GetProperty("format").GetString());
    }

    internal static WebApplicationFactory<business::Program> Factory(string? connectionString = null,
        ICustomerRepository? repository = null, Action<IWebHostBuilder>? configure = null) =>
        new WebApplicationFactory<business::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString ?? "Host=localhost;Database=unused;Username=unused",
                ["Jwt:Issuer"] = TestJwt.Issuer, ["Jwt:Audience"] = TestJwt.Audience, ["Jwt:SigningKey"] = TestJwt.SigningKey
            }));
            if (repository is not null)
                builder.ConfigureServices(services => services.RemoveAll<ICustomerRepository>().AddSingleton(repository));
            configure?.Invoke(builder);
        });

    internal static Dictionary<string, object?> Body(string name, string taxId, Guid version) => new()
    {
        ["name"] = name, ["customerType"] = "Individual", ["taxId"] = taxId,
        ["email"] = "update@example.com", ["version"] = version
    };

    internal static async Task<(string Path, JsonElement Customer)> CreateAsync(HttpClient client, string taxId)
    {
        using var response = await client.PostAsJsonAsync("/api/customers",
            new { name = "Old", tradeName = "Shop", customerType = "Company", taxId, email = "old@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (response.Headers.Location!.OriginalString, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    internal static Task<JsonElement> ReadAsync(HttpClient client, string path) => client.GetFromJsonAsync<JsonElement>(path);
    internal static Guid Version(JsonElement customer) => customer.GetProperty("version").GetGuid();

    internal static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string path)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(path, problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    internal sealed class FailingRepository : ICustomerRepository
    {
        public Task<bool> DeactivateAsync(Guid id, Guid expectedVersion, Guid newVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new InvalidOperationException("sensitive-repository-detail");
        public Task CreateAsync(Customer customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(Customer customer, Guid expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    internal sealed class RacingRepository(CustomerRepository inner) : ICustomerRepository
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<Guid> observedVersions = new();
        private int remaining;
        private int writes;

        public Guid[] ObservedVersions => observedVersions.ToArray();
        public int WriteAttempts => Volatile.Read(ref writes);
        public void Arm() => Volatile.Write(ref remaining, 2);

        public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var participates = Interlocked.Decrement(ref remaining) >= 0;
            var customer = await inner.GetByIdAsync(id, cancellationToken);
            if (participates)
            {
                observedVersions.Enqueue(customer!.Version);
                if (observedVersions.Count == 2)
                    gate.TrySetResult();
                await gate.Task.WaitAsync(cancellationToken);
            }
            return customer;
        }

        public Task<bool> UpdateAsync(Customer customer, Guid expectedVersion, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref writes);
            return inner.UpdateAsync(customer, expectedVersion, cancellationToken);
        }

        public Task CreateAsync(Customer customer, CancellationToken cancellationToken) => inner.CreateAsync(customer, cancellationToken);
        public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);
        public Guid? PersistedDeactivationVersion { get; private set; }

        public async Task<bool> DeactivateAsync(Guid id, Guid expectedVersion, Guid newVersion, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref writes);
            var persisted = await inner.DeactivateAsync(id, expectedVersion, newVersion, cancellationToken);
            if (persisted)
                PersistedDeactivationVersion = newVersion;
            return persisted;
        }
    }
}
