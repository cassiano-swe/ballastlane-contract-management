using BallastLane.Application.Exceptions;
using BallastLane.WebApi.Controllers;
using BallastLane.WebApi.Middleware;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BallastLane.WebApi.Extensions;

public sealed class UpdateCustomerDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CustomersController)
            || context.MethodInfo.Name != nameof(CustomersController.Update))
            return;

        operation.Responses["409"].Content["application/problem+json"].Examples = new Dictionary<string, OpenApiExample>
        {
            ["concurrency"] = Conflict(ExceptionHandlingMiddleware.ConcurrencyConflictType,
                ExceptionHandlingMiddleware.ConcurrencyConflictTitle, CustomerConcurrencyException.DefaultMessage),
            ["duplicateTaxId"] = Conflict(ExceptionHandlingMiddleware.DuplicateTaxIdType,
                ExceptionHandlingMiddleware.DuplicateTaxIdTitle, ExceptionHandlingMiddleware.DuplicateTaxIdDetail)
        };
    }

    private static OpenApiExample Conflict(string type, string title, string detail) => new()
    {
        Summary = title,
        Value = new OpenApiObject
        {
            ["type"] = new OpenApiString(type),
            ["title"] = new OpenApiString(title),
            ["status"] = new OpenApiInteger(409),
            ["detail"] = new OpenApiString(detail),
            ["instance"] = new OpenApiString("/api/customers/6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17"),
            ["traceId"] = new OpenApiString("example-trace-id")
        }
    };
}
