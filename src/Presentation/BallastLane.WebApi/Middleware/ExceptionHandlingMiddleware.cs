using FluentValidation;
using BallastLane.Application.Exceptions;
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
            if (context.Response.HasStarted) { logger.LogError("Response already started after exception of type {ExceptionType}; trace {TraceIdentifier}", exception.GetType().Name, context.TraceIdentifier); throw; }
            var validation = exception as ValidationException;
            var conflict = exception is CustomerAlreadyExistsException;
            var status = validation is not null ? StatusCodes.Status400BadRequest : conflict ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
            if (validation is null && !conflict) logger.LogError("Unhandled exception of type {ExceptionType}; trace {TraceIdentifier}", exception.GetType().Name, context.TraceIdentifier);
            ProblemDetails problem = validation is not null
                ? new ValidationProblemDetails(validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())) { Title = "Validation failed", Detail = string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct()) }
                : new ProblemDetails { Title = conflict ? "Conflict" : "An unexpected error occurred", Detail = conflict ? "A customer with the provided tax identifier already exists." : "An unexpected error occurred." };
            problem.Type = "about:blank"; problem.Status = status; problem.Instance = context.Request.Path;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            context.Response.Clear();
            context.Response.StatusCode = status;
            if (problem is ValidationProblemDetails validationProblem)
                await context.Response.WriteAsJsonAsync(validationProblem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
            else
                await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
