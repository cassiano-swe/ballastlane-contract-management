using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using Microsoft.AspNetCore.Http.Features;
using BallastLane.WebApi.Middleware;
using BallastLane.Application.Exceptions;
using System.Text.Json;
namespace BallastLane.WebApi.UnitTests;

public class AdditionalMiddlewareTests
{
    [Theory]
    [InlineData(200, LogLevel.Information)]
    [InlineData(400, LogLevel.Warning)]
    [InlineData(500, LogLevel.Error)]
    public async Task LogsStructuredCompletionWithoutSensitiveData(int status, LogLevel level)
    {
        var logger = new CaptureLogger<RequestLoggingMiddleware>();
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-123";
        context.Request.Method = "POST";
        context.Request.Path = "/example/private-id";
        context.Request.QueryString = new QueryString("?token=secret");
        context.Request.Headers.Authorization = "Bearer secret";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/example/{id}"), 0, EndpointMetadataCollection.Empty, "example"));
        await new RequestLoggingMiddleware(c => { c.Response.StatusCode = status; return Task.CompletedTask; }, logger).InvokeAsync(context);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(level, entry.Level);
        Assert.Equal("POST", entry.Values["Method"]);
        Assert.Equal("/example/{id}", entry.Values["Route"]);
        Assert.Equal(status, entry.Values["StatusCode"]);
        Assert.Equal("trace-123", entry.Values["TraceIdentifier"]);
        Assert.True((double)entry.Values["ElapsedMilliseconds"]! >= 0);
        Assert.DoesNotContain("secret", string.Join(" ", entry.Values.Values));
        Assert.DoesNotContain("private-id", string.Join(" ", entry.Values.Values));
    }

    [Fact] public async Task LogsCancellationAsWarningAndPropagates()
    {
        var logger = new CaptureLogger<RequestLoggingMiddleware>();
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource(); cts.Cancel(); context.RequestAborted = cts.Token;
        var middleware = new RequestLoggingMiddleware(_ => throw new OperationCanceledException(cts.Token), logger);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
        Assert.Equal(499, Assert.Single(logger.Entries).Values["StatusCode"]);
    }

    [Fact] public async Task ValidationFailureProducesTraceableProblem()
    {
        var context = new DefaultHttpContext(); context.TraceIdentifier = "trace-456"; context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new ValidationException("invalid"), new CaptureLogger<ExceptionHandlingMiddleware>());
        await middleware.InvokeAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        Assert.Contains("trace-456", body); Assert.Contains("Validation failed", body);
    }

    [Fact]
    public async Task MissingCustomerProducesSafeProblemWithoutAnUnhandledErrorLog()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "not-found-trace" };
        context.Request.Path = "/api/customers/10000000-0000-0000-0000-000000000001";
        using var body = new MemoryStream();
        context.Response.Body = body;
        var logger = new CaptureLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new CustomerNotFoundException(), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var problem = document.RootElement;
        Assert.Equal("about:blank", problem.GetProperty("type").GetString());
        Assert.Equal("Customer not found", problem.GetProperty("title").GetString());
        Assert.Equal("The requested customer was not found.", problem.GetProperty("detail").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal(context.Request.Path.Value, problem.GetProperty("instance").GetString());
        Assert.Equal(context.TraceIdentifier, problem.GetProperty("traceId").GetString());
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(true, "urn:ballastlane:customer-concurrency-conflict", "The customer has changed since it was retrieved. Retrieve the latest customer state before attempting the operation again.")]
    [InlineData(false, "urn:ballastlane:customer-duplicate-tax-id", "A customer with the provided tax identifier already exists.")]
    public async Task CustomerConflictsProduceDistinctSafeProblems(bool concurrency, string type, string detail)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "conflict-trace" };
        context.Request.Path = "/api/customers/10000000-0000-0000-0000-000000000001";
        using var body = new MemoryStream();
        context.Response.Body = body;
        var logger = new CaptureLogger<ExceptionHandlingMiddleware>();
        Exception exception = concurrency ? new CustomerConcurrencyException() : new CustomerAlreadyExistsException();
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(409, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var problem = document.RootElement;
        Assert.Equal(type, problem.GetProperty("type").GetString());
        Assert.Equal(detail, problem.GetProperty("detail").GetString());
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal(context.Request.Path.Value, problem.GetProperty("instance").GetString());
        Assert.Equal(context.TraceIdentifier, problem.GetProperty("traceId").GetString());
        Assert.Empty(logger.Entries);
    }

    [Fact] public async Task StartedResponsePropagatesExceptionWithoutRewriting()
    {
        var context = new DefaultHttpContext();
        var responseFeature = Substitute.For<IHttpResponseFeature>();
        responseFeature.HasStarted.Returns(true);
        context.Features.Set(responseFeature);
        var logger = new CaptureLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException("failure"), logger);
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }

    [Fact] public async Task ExceptionCancellationIsPropagated()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };
        var middleware = new ExceptionHandlingMiddleware(_ => throw new OperationCanceledException(cts.Token), new CaptureLogger<ExceptionHandlingMiddleware>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
    }
}

internal sealed class CaptureLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, Dictionary<string, object?> Values)> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
        Entries.Add((level, values.ToDictionary(pair => pair.Key, pair => pair.Value)));
    }
}
