using BallastLane.WebApi.Models.Requests.Customers;
using BallastLane.WebApi.Models.Responses.Customers;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using BallastLane.Domain.Enums;
using Swashbuckle.AspNetCore.SwaggerGen;
namespace BallastLane.WebApi.Extensions;
public sealed class CustomerSchemaFilter : ISchemaFilter
{
    internal const string ExampleVersion = "c53db246-a877-43e1-952d-90fdedfb6781";

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CustomerType)) { schema.Description = "Allowed values: Individual or Company. Numeric values are not accepted."; schema.Type = "string"; schema.Format = null; schema.Enum = new List<IOpenApiAny> { new OpenApiString("Individual"), new OpenApiString("Company") }; return; }
        if (context.Type == typeof(CreateCustomerResponse)
            || context.Type == typeof(ListCustomersResponse)
            || context.Type == typeof(GetCustomerByIdResponse))
        {
            schema.Properties["version"].Description = "The optimistic concurrency token representing the current persisted state of the customer. This token will be required by future update and delete operations.";
            schema.Properties["version"].Example = new OpenApiString(ExampleVersion);
        }

        if (context.Type != typeof(CreateCustomerRequest)) return;
        schema.Properties["name"].MaxLength = 200;
        schema.Properties["tradeName"].MaxLength = 200;
        schema.Properties["taxId"].MaxLength = 50;
        schema.Properties["email"].MaxLength = 254;
    }
}
