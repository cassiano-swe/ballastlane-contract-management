extern alias business;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BallastLane.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using static BallastLane.IntegrationTests.UpdateCustomerTests;

namespace BallastLane.IntegrationTests;

public class DeleteCustomerTests
{
    private const string ConflictType = "urn:ballastlane:customer-concurrency-conflict";

    [Fact]
    public async Task DeactivationRetainsCustomerAndTaxIdAndPutPreservesInactiveStatus()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        using var logs = new TestLogProvider();
        await using var factory = Factory(database.GetConnectionString(), configure: builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        var (path, created) = await CreateAsync(client, "001");
        var active = await ReadAsync(client, path);
        CustomerJsonAssertions.MatchesWriteResponse(created, active, active: true);
        var originalVersion = Version(active);
        var (otherPath, other) = await CreateAsync(client, "002");

        using var staleWhileActive = await client.DeleteAsync(DeleteUrl(path, Guid.NewGuid()));
        await AssertProblemAsync(staleWhileActive, HttpStatusCode.Conflict, path, logs, ConflictType);
        Assert.True(JsonElement.DeepEquals(active, await ReadAsync(client, path)));

        var deletedUrl = DeleteUrl(path, originalVersion);
        using var deleted = await client.DeleteAsync(deletedUrl);
        await AssertNoContentAsync(deleted);
        var inactive = await ReadAsync(client, path);
        Assert.False(inactive.GetProperty("active").GetBoolean());
        var inactiveVersion = Version(inactive);
        Assert.NotEqual(originalVersion, inactiveVersion);
        Assert.NotEqual(Guid.Empty, inactiveVersion);
        AssertBusinessState(active, inactive);
        var collection = await ReadAsync(client, "/api/customers");
        Assert.Equal(2, collection.GetArrayLength());
        Assert.Contains(collection.EnumerateArray(), item => JsonElement.DeepEquals(item, inactive));
        Assert.Contains(collection.EnumerateArray(), item => item.GetProperty("active").GetBoolean());
        var id = inactive.GetProperty("id").GetGuid();
        var persisted = await new CustomerRepository(source).GetByIdAsync(id, default);
        Assert.NotNull(persisted);
        Assert.False(persisted.Active);
        Assert.Equal(inactiveVersion, persisted.Version);

        using var repeated = await client.DeleteAsync(DeleteUrl(path, inactiveVersion));
        await AssertNoContentAsync(repeated);
        using var stale = await client.DeleteAsync(deletedUrl);
        await AssertProblemAsync(stale, HttpStatusCode.Conflict, path, logs, ConflictType);
        Assert.True(JsonElement.DeepEquals(inactive, await ReadAsync(client, path)));
        Assert.Equal(2, logs.Entries.Count(entry => IsDeleteLog(entry, 204)));

        using var duplicateCreate = await client.PostAsJsonAsync("/api/customers",
            new { name = "Duplicate", customerType = "Individual", taxId = "001", email = "dup@example.com" });
        using var duplicateUpdate = await client.PutAsJsonAsync(otherPath, Body("Duplicate", "001", Version(other)));
        foreach (var response in new[] { duplicateCreate, duplicateUpdate })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("urn:ballastlane:customer-duplicate-tax-id", problem.GetProperty("type").GetString());
        }

        var replacement = Body("Updated", "001", inactiveVersion);
        replacement["active"] = true;
        using var update = await client.PutAsJsonAsync(path, replacement);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated", updated.GetProperty("name").GetString());
        Assert.NotEqual(inactiveVersion, Version(updated));
        CustomerJsonAssertions.MatchesWriteResponse(updated, await ReadAsync(client, path), active: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingWritesWithSameVersionPersistExactlyOneState(bool competeWithPut)
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        var repository = new RacingRepository(new CustomerRepository(source));
        await using var factory = Factory(database.GetConnectionString(), repository);
        using var client = factory.CreateClient();
        var (path, created) = await CreateAsync(client, "001");
        var version = Version(created);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        repository.Arm();

        var responses = await Task.WhenAll(
            client.DeleteAsync(DeleteUrl(path, version), timeout.Token),
            competeWithPut
                ? client.PutAsJsonAsync(path, Body("Updated", "001", version), timeout.Token)
                : client.DeleteAsync(DeleteUrl(path, version), timeout.Token));
        using var first = responses[0];
        using var second = responses[1];
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var problem = await ProblemAsync(conflict, HttpStatusCode.Conflict, path);
        Assert.Equal(ConflictType, problem.GetProperty("type").GetString());
        Assert.Equal(new[] { version, version }, repository.ObservedVersions);
        Assert.Equal(2, repository.WriteAttempts);
        var final = await ReadAsync(client, path);
        Assert.NotEqual(version, Version(final));
        if (second.StatusCode == HttpStatusCode.OK)
        {
            Assert.True(competeWithPut);
            var updated = await second.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Updated", updated.GetProperty("name").GetString());
            CustomerJsonAssertions.MatchesWriteResponse(updated, final, active: true);
        }
        else
        {
            await AssertNoContentAsync(Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent));
            Assert.False(final.GetProperty("active").GetBoolean());
            Assert.Equal(repository.PersistedDeactivationVersion, Version(final));
            AssertBusinessState(created, final);
        }
    }

    [Theory]
    [InlineData("malformed-id")]
    [InlineData("empty-id")]
    [InlineData("missing-version")]
    [InlineData("malformed-version")]
    [InlineData("empty-version")]
    public async Task InvalidParametersReturnTraceable400(string scenario)
    {
        using var logs = new TestLogProvider();
        await using var factory = Factory(repository: new FailingRepository(), configure: builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        var id = scenario switch
        {
            "malformed-id" => "invalid",
            "empty-id" => Guid.Empty.ToString(),
            _ => Guid.NewGuid().ToString()
        };
        var query = scenario switch
        {
            "missing-version" => "",
            "malformed-version" => "?version=invalid-supplied-version",
            "empty-version" => $"?version={Guid.Empty}",
            _ => $"?version={Guid.NewGuid()}"
        };
        var path = $"/api/customers/{id}";

        using var response = await client.DeleteAsync(path + query);
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, path, logs);
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
        Assert.DoesNotContain("invalid-supplied-version", problem.GetRawText());
    }

    [Fact]
    public async Task MissingCustomerAndUnexpectedErrorUseExistingProblemDetails()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        using var logs = new TestLogProvider();
        void AddLogging(IWebHostBuilder builder) => builder.ConfigureLogging(logging => logging.AddProvider(logs));
        await using var factory = Factory(database.GetConnectionString(), configure: AddLogging);
        await using var failing = Factory(repository: new FailingRepository(), configure: AddLogging);
        using var client = factory.CreateClient();
        using var failingClient = failing.CreateClient();
        var path = $"/api/customers/{Guid.NewGuid()}";
        var url = DeleteUrl(path, Guid.NewGuid());

        using var missing = await client.DeleteAsync(url);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, path, logs);
        using var failed = await failingClient.DeleteAsync(url);
        var problem = await AssertProblemAsync(failed, HttpStatusCode.InternalServerError, path, logs);
        Assert.DoesNotContain("sensitive-repository-detail", problem.GetRawText());
    }

    [Fact]
    public async Task SwaggerDocumentsAnonymousDeleteAndActivationInGetSchemas()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        var swagger = await ReadAsync(client, "/swagger/v1/swagger.json");
        var operation = swagger.GetProperty("paths").GetProperty("/api/customers/{id}").GetProperty("delete");
        Assert.Equal("Deactivates an existing customer using soft delete and optimistic concurrency control.", operation.GetProperty("summary").GetString());
        Assert.False(operation.TryGetProperty("security", out _));
        Assert.False(operation.TryGetProperty("requestBody", out _));
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!);
        Assert.Equal(2, parameters.Count);
        foreach (var (name, location) in new[] { ("id", "path"), ("version", "query") })
        {
            var parameter = parameters[name];
            Assert.Equal(location, parameter.GetProperty("in").GetString());
            Assert.True(parameter.GetProperty("required").GetBoolean());
            Assert.Equal("uuid", parameter.GetProperty("schema").GetProperty("format").GetString());
        }
        var responses = operation.GetProperty("responses");
        Assert.Equal(new[] { "204", "400", "404", "409", "500" }, responses.EnumerateObject().Select(item => item.Name).Order().ToArray());
        Assert.False(responses.GetProperty("204").TryGetProperty("content", out _));

        var schemas = swagger.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "GetCustomerByIdResponse", "ListCustomersResponse" })
            Assert.Equal("boolean", schemas.GetProperty(name).GetProperty("properties").GetProperty("active").GetProperty("type").GetString());
        foreach (var name in new[] { "CreateCustomerRequest", "CreateCustomerResponse", "UpdateCustomerRequest", "UpdateCustomerResponse" })
            Assert.False(schemas.GetProperty(name).GetProperty("properties").TryGetProperty("active", out _));
    }

    private static string DeleteUrl(string path, Guid version) => $"{path}?version={version}";

    private static async Task AssertNoContentAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private static void AssertBusinessState(JsonElement expected, JsonElement actual)
    {
        foreach (var field in new[] { "id", "name", "tradeName", "customerType", "taxId", "email" })
            Assert.True(JsonElement.DeepEquals(expected.GetProperty(field), actual.GetProperty(field)), field);
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status,
        string path, TestLogProvider logs, string type = "about:blank")
    {
        var problem = await ProblemAsync(response, status, path);
        Assert.Equal(type, problem.GetProperty("type").GetString());
        Assert.Contains(logs.Entries, entry => IsDeleteLog(entry, (int)status)
            && Equals(entry.GetValueOrDefault("TraceIdentifier"), problem.GetProperty("traceId").GetString()));
        return problem;
    }

    private static bool IsDeleteLog(IReadOnlyDictionary<string, object?> entry, int status) =>
        Equals(entry.GetValueOrDefault("Method"), "DELETE")
        && Equals(entry.GetValueOrDefault("Route"), "api/customers/{id}")
        && Equals(entry.GetValueOrDefault("StatusCode"), status)
        && entry.ContainsKey("ElapsedMilliseconds")
        && entry.GetValueOrDefault("TraceIdentifier") is string { Length: > 0 };
}
