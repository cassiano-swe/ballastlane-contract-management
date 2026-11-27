using BallastLane.WebApi.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BallastLane.WebApi.Extensions;

public sealed class ListCustomersDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CustomersController)
            || context.MethodInfo.Name != nameof(CustomersController.List))
            return;

        var json = operation.Responses["200"].Content["application/json"];
        json.Example = new OpenApiArray
        {
            new OpenApiObject
            {
                ["id"] = new OpenApiString("6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17"),
                ["name"] = new OpenApiString("Alex Taylor"),
                ["tradeName"] = new OpenApiNull(),
                ["customerType"] = new OpenApiString("Individual"),
                ["taxId"] = new OpenApiString("00123456789"),
                ["email"] = new OpenApiString("alex@example.com"),
                ["version"] = new OpenApiString(CustomerSchemaFilter.ExampleVersion)
            }
        };
    }
}
