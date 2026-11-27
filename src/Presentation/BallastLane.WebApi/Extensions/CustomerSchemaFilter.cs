using BallastLane.WebApi.Models.Requests.Customers;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using BallastLane.Domain.Enums;
using Swashbuckle.AspNetCore.SwaggerGen;
namespace BallastLane.WebApi.Extensions;
public sealed class CustomerSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CustomerType)) { schema.Description = "Allowed values: Individual or Company. Numeric values are not accepted."; schema.Type = "string"; schema.Format = null; schema.Enum = new List<IOpenApiAny> { new OpenApiString("Individual"), new OpenApiString("Company") }; return; }
        if (context.Type != typeof(CreateCustomerRequest)) return;
        schema.Properties["name"].MaxLength = 200;
        schema.Properties["tradeName"].MaxLength = 200;
        schema.Properties["taxId"].MaxLength = 50;
        schema.Properties["email"].MaxLength = 254;
    }
}
