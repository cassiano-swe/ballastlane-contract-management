using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace BallastLane.IdentityApi.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceIdentifier"] = context.TraceIdentifier });
        var aborted = false;
        var failed = false;
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { aborted = true; throw; }
        catch { failed = true; throw; }
        finally
        {
            aborted |= context.RequestAborted.IsCancellationRequested;
            var status = aborted ? 499 : failed && !context.Response.HasStarted ? 500 : context.Response.StatusCode;
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "<unmatched>";
            var level = failed || status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level, "HTTP {Method} {Route} returned {StatusCode} in {ElapsedMilliseconds} ms; trace {TraceIdentifier}", context.Request.Method, route, status, Stopwatch.GetElapsedTime(start).TotalMilliseconds, context.TraceIdentifier);
        }
    }
}
