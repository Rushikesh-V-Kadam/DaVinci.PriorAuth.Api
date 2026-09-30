using System.Text.Json.Serialization;

namespace DaVinci.PriorAuth.Models.Fhir;

// Lightweight FHIR R4 data types. Property names serialize to camelCase, which matches
// FHIR JSON (e.g. ValueCodeableConcept -> "valueCodeableConcept"). Empty lists and null
// values are never written (see FhirJson).

public class Extension
{
    public string Url { get; set; } = string.Empty;

    /// <summary>Nested extensions (complex extension). C# cannot name a member like its class.</summary>
    [JsonPropertyName("extension")]
    public List<Extension> Extension_ { get; set; } = new();

    // value[x] - only one is set at a time
    public string? ValueString { get; set; }
    public string? ValueCode { get; set; }
    public bool? ValueBoolean { get; set; }
    public int? ValueInteger { get; set; }
    public int? ValuePositiveInt { get; set; }
    public decimal? ValueDecimal { get; set; }
    public string? ValueDate { get; set; }
    public string? ValueDateTime { get; set; }
    public string? ValueUrl { get; set; }
    public string? ValueUri { get; set; }
    public string? ValueCanonical { get; set; }
    public Coding? ValueCoding { get; set; }
    public CodeableConcept? ValueCodeableConcept { get; set; }
    public ResourceReference? ValueReference { get; set; }
    public Identifier? ValueIdentifier { get; set; }
    public Period? ValuePeriod { get; set; }
    public Quantity? ValueQuantity { get; set; }
    public ContactPoint? ValueContactPoint { get; set; }
    public Expression? ValueExpression { get; set; }

    public Extension() { }
    public Extension(string url) { Url = url; }

    public static Extension Code(string url, string code) => new(url) { ValueCode = code };
    public static Extension String(string url, string value) => new(url) { ValueString = value };
    public static Extension Bool(string url, bool value) => new(url) { ValueBoolean = value };
    public static Extension Canonical(string url, string value) => new(url) { ValueCanonical = value };
    public static Extension Date(string url, string value) => new(url) { ValueDate = value };
    public static Extension Ref(string url, ResourceReference value) => new(url) { ValueReference = value };
    public static Extension Concept(string url, CodeableConcept value) => new(url) { ValueCodeableConcept = value };
    public static Extension Complex(string url, params Extension[] parts)
    {
        var e = new Extension(url);
        e.Extension_.AddRange(parts);
        return e;
    }
}

public class Coding
{
    public string? System { get; set; }
    public string? Version { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }

    public Coding() { }
    public Coding(string system, string code, string? display = null)
    {
        System = system; Code = code; Display = display;
    }
}

public class CodeableConcept
{
    public List<Coding> Coding { get; set; } = new();
    public string? Text { get; set; }

    public CodeableConcept() { }
    public CodeableConcept(string system, string code, string? display = null, string? text = null)
    {
        Coding.Add(new Coding(system, code, display));
        Text = text;
    }

    public static CodeableConcept FromText(string text) => new() { Text = text };

    public bool HasCode(string system, string code) =>
        Coding.Any(c => c.System == system && c.Code == code);

    public bool HasCode(string code) => Coding.Any(c => c.Code == code);

    public Coding? First(string? system = null) =>
        Coding.FirstOrDefault(c => system == null || c.System == system);
}

/// <summary>FHIR Reference (named ResourceReference because a member cannot share its class name).</summary>
public class ResourceReference
{
    public string? Reference { get; set; }
    public string? Type { get; set; }
    public Identifier? Identifier { get; set; }
    public string? Display { get; set; }

    public ResourceReference() { }
    public ResourceReference(string reference, string? display = null)
    {
        Reference = reference; Display = display;
    }

    public static ResourceReference ByIdentifier(string system, string value, string? type = null) =>
        new() { Identifier = new Identifier(system, value), Type = type };
}

public class Identifier
{
    public string? Use { get; set; }
    public CodeableConcept? Type { get; set; }
    public string? System { get; set; }
    public string? Value { get; set; }
    public Period? Period { get; set; }
    public ResourceReference? Assigner { get; set; }

    public Identifier() { }
    public Identifier(string system, string value) { System = system; Value = value; }

    /// <summary>"system|value" - used as a lookup key.</summary>
    public string Key() => $"{System}|{Value}";
}

public class Period
{
    public string? Start { get; set; }
    public string? End { get; set; }
}

public class Quantity
{
    public decimal? Value { get; set; }
    public string? Comparator { get; set; }
    public string? Unit { get; set; }
    public string? System { get; set; }
    public string? Code { get; set; }
}

public class Money
{
    public decimal? Value { get; set; }
    public string? Currency { get; set; }
}

public class ContactPoint
{
    public string? System { get; set; }
    public string? Value { get; set; }
    public string? Use { get; set; }
}

public class HumanName
{
    public string? Use { get; set; }
    public string? Text { get; set; }
    public string? Family { get; set; }
    public List<string> Given { get; set; } = new();
}

public class Address
{
    public List<string> Line { get; set; } = new();
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class Attachment
{
    public string? ContentType { get; set; }
    public string? Language { get; set; }
    public string? Data { get; set; }   // base64
    public string? Url { get; set; }
    public int? Size { get; set; }
    public string? Title { get; set; }
    public string? Creation { get; set; }
}

public class Meta
{
    public string? VersionId { get; set; }
    public string? LastUpdated { get; set; }
    public List<string> Profile { get; set; } = new();
    public List<Coding> Tag { get; set; } = new();
}

public class Narrative
{
    public string Status { get; set; } = "generated";
    public string Div { get; set; } = "<div xmlns=\"http://www.w3.org/1999/xhtml\"></div>";
}

public class Expression
{
    public string? Description { get; set; }
    public string? Name { get; set; }
    public string Language { get; set; } = string.Empty;
    [JsonPropertyName("expression")]
    public string? ExpressionValue { get; set; }
    public string? Reference { get; set; }
}

/// <summary>Base for backbone elements that can carry extensions.</summary>
public abstract class BackboneElement
{
    public string? Id { get; set; }
    public List<Extension> Extension { get; set; } = new();
    public List<Extension> ModifierExtension { get; set; } = new();
}

/// <summary>Element-level extension holder for primitive "_element" properties.</summary>
public class PrimitiveExtensions
{
    public List<Extension> Extension { get; set; } = new();
}
