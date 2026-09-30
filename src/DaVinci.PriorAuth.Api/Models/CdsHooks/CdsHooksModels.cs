using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DaVinci.PriorAuth.Models.CdsHooks;

// CDS Hooks 2.0 wire models used by CRD. These are plain JSON (application/json), not FHIR.
// Serialized with CdsJson.Options (camelCase, nulls omitted).

// ---------------------------------------------------------------- Discovery
public class CdsDiscoveryResponse
{
    public List<CdsServiceDefinition> Services { get; set; } = new();
}

public class CdsServiceDefinition
{
    public string Hook { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string>? Prefetch { get; set; }
    public string? UsageRequirements { get; set; }

    /// <summary>Carries "davinci-crd.configuration-options".</summary>
    public Dictionary<string, object>? Extension { get; set; }
}

/// <summary>An entry of davinci-crd.configuration-options.</summary>
public class CrdConfigurationOption
{
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = "boolean"; // boolean | integer
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object Default { get; set; } = true;
}

// ---------------------------------------------------------------- Request
public class CdsHookRequest
{
    public string Hook { get; set; } = string.Empty;
    public string HookInstance { get; set; } = string.Empty;
    public string? FhirServer { get; set; }
    public FhirAuthorization? FhirAuthorization { get; set; }
    public JsonElement Context { get; set; }
    public Dictionary<string, JsonElement?>? Prefetch { get; set; }
    public JsonElement? Extension { get; set; }

    public string? ContextString(string name) =>
        Context.ValueKind == JsonValueKind.Object && Context.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public JsonElement? ContextElement(string name) =>
        Context.ValueKind == JsonValueKind.Object && Context.TryGetProperty(name, out var v) ? v : null;

    public JsonElement? PrefetchElement(string key) =>
        Prefetch != null && Prefetch.TryGetValue(key, out var v) && v.HasValue && v.Value.ValueKind == JsonValueKind.Object
            ? v : null;
}

/// <summary>OAuth token the EHR hands to the CDS service so it can query the EHR FHIR server.</summary>
public class FhirAuthorization
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "Bearer";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
}

// ---------------------------------------------------------------- Response
public class CdsHookResponse
{
    public List<CdsCard> Cards { get; set; } = new();
    public List<CdsAction>? SystemActions { get; set; }
}

public class CdsCard
{
    public string Uuid { get; set; } = Guid.NewGuid().ToString();
    public string Summary { get; set; } = string.Empty;   // < 140 chars
    public string? Detail { get; set; }                    // GitHub-flavored markdown
    public string Indicator { get; set; } = "info";        // info | warning | critical
    public CdsSource Source { get; set; } = new();
    public List<CdsSuggestion>? Suggestions { get; set; }
    public string? SelectionBehavior { get; set; }         // at-most-one | any
    public List<CdsLink>? Links { get; set; }
}

public class CdsSource
{
    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public CdsCoding? Topic { get; set; }
}

/// <summary>CDS Hooks Coding (card.source.topic) - CRD uses the CRD card-type codes.</summary>
public class CdsCoding
{
    public string System { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Display { get; set; }
}

public class CdsSuggestion
{
    public string Label { get; set; } = string.Empty;
    public string Uuid { get; set; } = Guid.NewGuid().ToString();
    public bool? IsRecommended { get; set; }
    public List<CdsAction>? Actions { get; set; }
}

public class CdsAction
{
    public string Type { get; set; } = "update";   // create | update | delete
    public string Description { get; set; } = string.Empty;
    /// <summary>FHIR resource as raw JSON so the EHR's original order is echoed unchanged.</summary>
    public JsonNode? Resource { get; set; }
    public string? ResourceId { get; set; }
}

public class CdsLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = "absolute";   // absolute | smart
    /// <summary>Only for type=smart. A string passed to the SMART app at launch.</summary>
    public string? AppContext { get; set; }
}

public static class CdsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
