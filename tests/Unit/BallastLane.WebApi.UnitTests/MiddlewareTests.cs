using BallastLane.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using NSubstitute;

namespace BallastLane.WebApi.UnitTests;

public class MiddlewareTests
{
    [Fact]
    public async Task RequestLoggingInvokesNext()
    {
        var next = Substitute.For<RequestDelegate>();
        var context = new DefaultHttpContext();
        next(context).Returns(Task.CompletedTask);
        var middleware = new RequestLoggingMiddleware(next, NullLogger<RequestLoggingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        await next.Received(1)(context);
    }

    [Fact]
    public async Task ExceptionHandlerReturnsSafeProblem()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new Exception("secret"), NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        Assert.DoesNotContain("secret", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }
}
