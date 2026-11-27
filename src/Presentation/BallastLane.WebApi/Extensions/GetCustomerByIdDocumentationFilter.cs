using BallastLane.WebApi.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BallastLane.WebApi.Extensions;

public sealed class GetCustomerByIdDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(CustomersController)
            || context.MethodInfo.Name != nameof(CustomersController.GetById))
            return;

        operation.Responses["200"].Content["application/json"].Example = new OpenApiObject
        {
            ["id"] = new OpenApiString("6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17"),
            ["name"] = new OpenApiString("Acme Corporation"),
            ["tradeName"] = new OpenApiString("Acme"),
            ["customerType"] = new OpenApiString("Company"),
            ["taxId"] = new OpenApiString("00123456789"),
            ["email"] = new OpenApiString("contact@acme.example")
        };
    }
}
