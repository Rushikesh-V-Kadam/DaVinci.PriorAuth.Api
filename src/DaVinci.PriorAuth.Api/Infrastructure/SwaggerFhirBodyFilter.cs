using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DaVinci.PriorAuth.Infrastructure;

/// <summary>
/// The FHIR endpoints read the raw body (no [FromBody]), so Swagger would show no body box.
/// This adds a JSON body box to every POST/PUT that doesn't already have one.
/// </summary>
public class SwaggerFhirBodyFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.ApiDescription.HttpMethod;
        if ((method == "POST" || method == "PUT") && operation.RequestBody == null)
        {
            var body = new OpenApiRequestBody
            {
                Required = true,
                Description = "Paste a FHIR resource (JSON) here - see the samples folder"
            };
            body.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() }
            };
            operation.RequestBody = body;
        }
    }
}