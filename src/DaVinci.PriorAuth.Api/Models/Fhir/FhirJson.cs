using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DaVinci.PriorAuth.Models.Fhir;

/// <summary>
/// FHIR JSON serialization with System.Text.Json:
///  - camelCase names, nulls and empty arrays omitted (FHIR forbids empty arrays)
///  - polymorphic Resource handling driven by "resourceType"
///  - unknown resource types kept as GenericResource so nothing is lost
/// </summary>
public static class FhirJson
{
    public static readonly JsonSerializerOptions Options = Create(indented: true);
    public static readonly JsonSerializerOptions Compact = Create(indented: false);

    private static JsonSerializerOptions Create(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = false,
            WriteIndented = indented,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipEmptyCollections } }
        };
        options.Converters.Add(new ResourceJsonConverter());
        return options;
    }

    private static void SkipEmptyCollections(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType == typeof(string) || property.IsExtensionData) continue;
            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = (_, value) => value is ICollection c ? c.Count > 0 : value != null;
            }
        }
    }

    public static string Serialize(Resource resource, bool indented = true) =>
        JsonSerializer.Serialize(resource, typeof(Resource), indented ? Options : Compact);

    public static Resource? Parse(string json) =>
        JsonSerializer.Deserialize<Resource>(json, Options);

    public static T Parse<T>(string json) where T : Resource =>
        Parse(json) as T ?? throw new JsonException($"Expected a {typeof(T).Name} resource.");

    public static Resource? Parse(JsonElement element) =>
        element.Deserialize<Resource>(Options);

    public static T? Parse<T>(JsonNode? node) where T : Resource =>
        node == null ? null : node.Deserialize<Resource>(Options) as T;

    /// <summary>Converts a typed model into a mutable JSON tree (used for CDS Hooks echo updates).</summary>
    public static JsonObject ToNode(Resource resource) =>
        JsonNode.Parse(Serialize(resource, false))!.AsObject();

    public static readonly Dictionary<string, Type> KnownTypes = new()
    {
        ["Bundle"] = typeof(Bundle),
        ["Parameters"] = typeof(Parameters),
        ["OperationOutcome"] = typeof(OperationOutcome),
        ["Patient"] = typeof(Patient),
        ["Organization"] = typeof(Organization),
        ["Practitioner"] = typeof(Practitioner),
        ["PractitionerRole"] = typeof(PractitionerRole),
        ["Coverage"] = typeof(Coverage),
        ["Claim"] = typeof(Claim),
        ["ClaimResponse"] = typeof(ClaimResponse),
        ["Questionnaire"] = typeof(Questionnaire),
        ["QuestionnaireResponse"] = typeof(QuestionnaireResponse),
        ["Library"] = typeof(Library),
        ["ValueSet"] = typeof(ValueSet),
        ["Task"] = typeof(FhirTask),
        ["CommunicationRequest"] = typeof(CommunicationRequest),
        ["DocumentReference"] = typeof(DocumentReference),
        ["Subscription"] = typeof(Subscription),
    };
}

/// <summary>Reads/writes the abstract Resource type using the "resourceType" discriminator.</summary>
public sealed class ResourceJsonConverter : JsonConverter<Resource>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Resource);

    public override Resource? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("resourceType", out var rtElement) ||
            rtElement.GetString() is not { Length: > 0 } resourceType)
        {
            throw new JsonException("FHIR resource is missing 'resourceType'.");
        }

        if (FhirJson.KnownTypes.TryGetValue(resourceType, out var clrType))
        {
            // Concrete types are not handled by this converter, so there is no recursion.
            return (Resource?)root.Deserialize(clrType, options);
        }

        var generic = new GenericResource(resourceType);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name != "resourceType") generic.Data[property.Name] = property.Value.Clone();
        }
        return generic;
    }

    public override void Write(Utf8JsonWriter writer, Resource value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

/// <summary>Helpers for reading and writing FHIR extensions.</summary>
public static class ExtensionHelpers
{
    public static Extension? GetByUrl(this IEnumerable<Extension> extensions, string url) =>
        extensions.FirstOrDefault(e => e.Url == url);

    public static IEnumerable<Extension> GetAllByUrl(this IEnumerable<Extension> extensions, string url) =>
        extensions.Where(e => e.Url == url);

    public static string? CodeValue(this IEnumerable<Extension> extensions, string url) =>
        extensions.GetByUrl(url)?.ValueCode;

    /// <summary>First coding code of a CodeableConcept-valued extension.</summary>
    public static string? ConceptCode(this IEnumerable<Extension> extensions, string url) =>
        extensions.GetByUrl(url)?.ValueCodeableConcept?.Coding.FirstOrDefault()?.Code;

    public static bool IsTrue(this IEnumerable<Extension> extensions, string url) =>
        extensions.GetByUrl(url)?.ValueBoolean == true;
}

/// <summary>FHIR date/dateTime formatting helpers (FHIR uses ISO-8601 strings).</summary>
public static class FhirDate
{
    public static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
    public static string Today() => System.DateTime.UtcNow.ToString("yyyy-MM-dd");
    public static string Date(System.DateTime value) => value.ToString("yyyy-MM-dd");
    public static string Date(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd");
    public static string Instant(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public static DateTimeOffset? TryParse(string? value) =>
        DateTimeOffset.TryParse(value, out var d) ? d : null;
}
