using FluentValidation;
using BallastLane.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace BallastLane.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    internal const string ConcurrencyConflictType = "urn:ballastlane:customer-concurrency-conflict";
    internal const string DuplicateTaxIdType = "urn:ballastlane:customer-duplicate-tax-id";
    internal const string ConcurrencyConflictTitle = "Customer concurrency conflict";
    internal const string DuplicateTaxIdTitle = "Conflict";
    internal const string DuplicateTaxIdDetail = "A customer with the provided tax identifier already exists.";

    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) { logger.LogError("Response already started after exception of type {ExceptionType}; trace {TraceIdentifier}", exception.GetType().Name, context.TraceIdentifier); throw; }
            var validation = exception as ValidationException;
            var (status, type, title, detail) = exception switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, "about:blank", "Validation failed", ""),
                CustomerAlreadyExistsException => (StatusCodes.Status409Conflict, DuplicateTaxIdType, DuplicateTaxIdTitle, DuplicateTaxIdDetail),
                CustomerConcurrencyException conflict => (StatusCodes.Status409Conflict, ConcurrencyConflictType, ConcurrencyConflictTitle, conflict.Message),
                CustomerNotFoundException notFound => (StatusCodes.Status404NotFound, "about:blank", "Customer not found", notFound.Message),
                _ => (StatusCodes.Status500InternalServerError, "about:blank", "An unexpected error occurred", "An unexpected error occurred.")
            };
            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError("Unhandled exception of type {ExceptionType}; trace {TraceIdentifier}", exception.GetType().Name, context.TraceIdentifier);

            ProblemDetails problem = validation is not null
                ? new ValidationProblemDetails(validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
                : new ProblemDetails();
            problem.Title = title;
            problem.Detail = validation is null ? detail : string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct());
            problem.Type = type; problem.Status = status; problem.Instance = context.Request.Path;
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
