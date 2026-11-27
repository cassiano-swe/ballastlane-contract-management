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
                ["version"] = new OpenApiString(CustomerSchemaFilter.ExampleVersion),
                ["active"] = new OpenApiBoolean(true)
            },
            new OpenApiObject
            {
                ["id"] = new OpenApiString("c4c1e2b1-2193-41af-90dd-120e9f1f85b4"),
                ["name"] = new OpenApiString("Example Corporation"),
                ["tradeName"] = new OpenApiString("Example"),
                ["customerType"] = new OpenApiString("Company"),
                ["taxId"] = new OpenApiString("987654321"),
                ["email"] = new OpenApiString("contact@example.com"),
                ["version"] = new OpenApiString("0f3c1e87-8923-4e25-925e-46a083f113e4"),
                ["active"] = new OpenApiBoolean(false)
            }
        };
    }
}
