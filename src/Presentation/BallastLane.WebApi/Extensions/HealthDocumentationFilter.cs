using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BallastLane.WebApi.Extensions;

public sealed class HealthDocumentationFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        document.Paths["/health"] = new OpenApiPathItem
        {
            Operations = new Dictionary<OperationType, OpenApiOperation>
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Summary = "Checks API process health.",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Healthy" },
                        ["503"] = new OpenApiResponse { Description = "Unhealthy" }
                    }
                }
            }
        };
    }
}
