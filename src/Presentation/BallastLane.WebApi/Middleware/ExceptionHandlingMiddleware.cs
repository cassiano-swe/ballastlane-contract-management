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
            var (status, title, detail) = exception switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", ""),
                CustomerAlreadyExistsException => (StatusCodes.Status409Conflict, "Conflict", "A customer with the provided tax identifier already exists."),
                CustomerNotFoundException notFound => (StatusCodes.Status404NotFound, "Customer not found", notFound.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "An unexpected error occurred.")
            };
            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError("Unhandled exception of type {ExceptionType}; trace {TraceIdentifier}", exception.GetType().Name, context.TraceIdentifier);

            ProblemDetails problem = validation is not null
                ? new ValidationProblemDetails(validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
                : new ProblemDetails();
            problem.Title = title;
            problem.Detail = validation is null ? detail : string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct());
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
