using BallastLane.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BallastLane.IdentityApi.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) { logger.LogError("Response already started after {ExceptionType}; trace {TraceIdentifier}", exception.GetType().Name, context.TraceIdentifier); throw; }
            var (status, title, detail) = exception switch
            {
                ValidationException => (400, "Validation failed", "One or more inputs are invalid."),
                InvalidCredentialsException => (401, "Authentication failed", "Invalid email or password."),
                _ => (500, "An unexpected error occurred", "An unexpected error occurred.")
            };
            if (status == 500)
                logger.LogError("Unhandled {ExceptionType}; trace {TraceIdentifier}", exception.GetType().Name, context.TraceIdentifier);
            var problem = new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{status}",
                Title = title,
                Status = status,
                Detail = detail,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
