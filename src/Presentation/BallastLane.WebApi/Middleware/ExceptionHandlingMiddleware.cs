using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BallastLane.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) { logger.LogError(exception, "Response already started; trace {TraceIdentifier}", context.TraceIdentifier); throw; }
            var validation = exception is ValidationException;
            var status = validation ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
            if (!validation) logger.LogError(exception, "Unhandled exception; trace {TraceIdentifier}", context.TraceIdentifier);
            var problem = new ProblemDetails { Type = validation ? "https://httpstatuses.com/400" : "https://httpstatuses.com/500", Title = validation ? "Validation failed" : "An unexpected error occurred", Status = status, Detail = validation ? "One or more inputs are invalid." : "An unexpected error occurred.", Instance = context.Request.Path };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
