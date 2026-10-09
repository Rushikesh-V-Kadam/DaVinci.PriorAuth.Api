using System.Text.Json;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.Fhir;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Infrastructure;

/// <summary>Writes FHIR resources as application/fhir+json.</summary>
public static class FhirResults
{
    public static ContentResult Fhir(Resource resource, int statusCode = 200) => new()
    {
        Content = FhirJson.Serialize(resource),
        ContentType = MediaTypes.FhirJson,
        StatusCode = statusCode
    };

    public static ContentResult Error(int statusCode, string code, string message, string? expression = null) =>
        Fhir(OperationOutcome.Create(IssueSeverity.Error, code, message, expression), statusCode);
}

/// <summary>Reads the raw request body and parses it as a FHIR resource (application/fhir+json or application/json).</summary>
public static class FhirRequest
{
    public static async Task<(T? Resource, ContentResult? Error)> ReadAsync<T>(HttpRequest request) where T : Resource
    {
        string body;
        using (var reader = new StreamReader(request.Body))
            body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return (null, FhirResults.Error(400, IssueType.Required, "Request body is empty."));

        try
        {
            var resource = FhirJson.Parse(body);
            if (resource is T typed) return (typed, null);
            return (null, FhirResults.Error(400, IssueType.Invalid,
                $"Expected a {typeof(T).Name} resource but received {resource?.ResourceType ?? "nothing"}."));
        }
        catch (JsonException ex)
        {
            return (null, FhirResults.Error(400, IssueType.Invalid, $"Invalid FHIR JSON: {ex.Message}"));
        }
    }

    /// <summary>
    /// FHIR operations with a single resource input can be called with that resource as the body,
    /// or with a Parameters resource. This accepts both. Several parameter names can be given
    /// (the first one that is present is used), for operations whose input name differs between versions.
    /// </summary>
    public static async Task<(T? Resource, ContentResult? Error)> ReadOperationInputAsync<T>(HttpRequest request, params string[] parameterNames)
        where T : Resource
    {
        var (resource, error) = await ReadAsync<Resource>(request);
        if (error != null) return (null, error);
        if (resource is T typed) return (typed, null);
        if (resource is Parameters p)
        {
            foreach (var parameterName in parameterNames)
            {
                if (p.GetSingle(parameterName)?.Resource is T inner) return (inner, null);
            }
        }
        return (null, FhirResults.Error(400, IssueType.Invalid,
            $"Body must be a {typeof(T).Name} or a Parameters resource with a '{string.Join("' or '", parameterNames)}' parameter."));
    }
}

// ------------------------------------------------------------------ scope-based authorization

/// <summary>Requires one of the listed SMART scopes in the access token's "scope" claim.</summary>
public class ScopeRequirement : IAuthorizationRequirement
{
    public string[] Scopes { get; }
    public ScopeRequirement(params string[] scopes) { Scopes = scopes; }
}

public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    private readonly PayerOptions _options;
    public ScopeAuthorizationHandler(IOptions<PayerOptions> options) { _options = options.Value; }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (!_options.RequireAuthentication)
        {
            context.Succeed(requirement);   // local development only
            return Task.CompletedTask;
        }

        var granted = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet();
        // no scopes listed = any authenticated caller (used for CDS Hooks JWTs)
        if (context.User.Identity?.IsAuthenticated == true &&
            (requirement.Scopes.Length == 0 || requirement.Scopes.Any(granted.Contains)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
