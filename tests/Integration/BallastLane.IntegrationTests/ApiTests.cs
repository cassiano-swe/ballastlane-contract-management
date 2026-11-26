extern alias business;
extern alias identity;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Npgsql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class ApiTests
{
    private static WebApplicationFactory<T> Factory<T>(string environment = "Development", ILoggerProvider? provider = null, bool configureJwt = true) where T : class => new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, ExceptionTestFilter>());
        if (provider is not null) builder.ConfigureLogging(logging => logging.AddProvider(provider));
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = configureJwt ? "tests" : "", ["Jwt:Audience"] = configureJwt ? "tests" : "", ["Jwt:SigningKey"] = configureJwt ? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") : "",
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        }));
    });

    [Fact] public async Task BusinessApiWorksInDevelopmentWithoutJwtConfiguration()
    {
        await using var app = Factory<business::Program>(configureJwt: false);
        var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/examples/calculate-percentage", new { value = 200, percentage = 25 })).StatusCode);
    }

    [Fact] public async Task BusinessApiRejectsMissingJwtConfigurationOutsideDevelopment()
    {
        await using var app = Factory<business::Program>("Production", configureJwt: false);
        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());
        Assert.Contains("Jwt:Issuer", error.Message);
    }

    [Fact] public async Task BothApisExposeHealthAndDevelopmentSwagger()
    {
        await using var business = Factory<business::Program>();
        await using var identity = Factory<identity::Program>();
        foreach (var client in new[] { business.CreateClient(), identity.CreateClient() })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            var swagger = await client.GetAsync("/swagger/v1/swagger.json");
            Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
            Assert.Contains("/health", await swagger.Content.ReadAsStringAsync());
        }
    }

    [Fact] public async Task HttpRequestsAreNotRedirectedToHttps()
    {
        await using var business = Factory<business::Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["https_port"] = "7999" })));
        await using var identity = Factory<identity::Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["https_port"] = "7999" })));
        foreach (var client in new[] { business.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }), identity.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }) })
        {
            var response = await client.GetAsync("http://localhost/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }
    }

    [Fact] public async Task ExampleCalculatesAndValidationReturnsProblem()
    {
        await using var app = Factory<business::Program>();
        var client = app.CreateClient();
        var ok = await client.PostAsJsonAsync("/api/examples/calculate-percentage", new { value = 200, percentage = 25 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("50", await ok.Content.ReadAsStringAsync());
        var invalid = await client.PostAsJsonAsync("/api/examples/calculate-percentage", new { value = -1, percentage = 25 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
    }

    [Fact] public async Task ExampleIsUnavailableInProduction()
    {
        await using var app = Factory<business::Program>("Production");
        Assert.Equal(HttpStatusCode.NotFound, (await app.CreateClient().PostAsJsonAsync("/api/examples/calculate-percentage", new { value = 1, percentage = 1 })).StatusCode);
    }

    [Fact] public async Task UnexpectedFailureReturnsSafeTraceableProblem()
    {
        using var logs = new TestLogProvider();
        await using var app = Factory<business::Program>(provider: logs);
        var response = await app.CreateClient().GetAsync("/__test_exception");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("test-sensitive-detail", body);
        var trace = JsonDocument.Parse(body).RootElement.GetProperty("traceId").GetString();
        Assert.Contains(logs.Entries, values => values.TryGetValue("StatusCode", out var status) && Equals(status, 500)
            && values.TryGetValue("TraceIdentifier", out var loggedTrace) && Equals(loggedTrace, trace));
    }

    [Fact] public async Task ConnectsToPostgres()
    {
        await using var container = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await container.StartAsync();
        await using var dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await using var connection = await dataSource.OpenConnectionAsync();
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }
}

internal sealed class TestLogProvider : ILoggerProvider
{
    public ConcurrentBag<Dictionary<string, object?>> Entries { get; } = [];
    public ILogger CreateLogger(string categoryName) => new TestLogger(Entries);
    public void Dispose() { }
    private sealed class TestLogger(ConcurrentBag<Dictionary<string, object?>> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
                entries.Add(values.ToDictionary(item => item.Key, item => item.Value));
        }
    }
}

internal sealed class ExceptionTestFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path == "/__test_exception") throw new InvalidOperationException("test-sensitive-detail");
            await nextMiddleware(context);
        });
    };
}
