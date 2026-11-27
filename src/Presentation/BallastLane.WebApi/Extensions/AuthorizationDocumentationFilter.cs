using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BallastLane.WebApi.Extensions;

public sealed class AuthorizationDocumentationFilter(IOptions<AuthorizationOptions> authorization) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any()
            || (!metadata.OfType<IAuthorizeData>().Any() && authorization.Value.FallbackPolicy is null))
            return;
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
        });
        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "A valid Bearer access token is required.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
                }
            },
            Headers = new Dictionary<string, OpenApiHeader>
            {
                ["WWW-Authenticate"] = new()
                {
                    Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString("Bearer") }
                }
            }
        });
    }
}
