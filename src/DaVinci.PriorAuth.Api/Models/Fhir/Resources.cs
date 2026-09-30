using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaVinci.PriorAuth.Models.Fhir;

// Base classes -----------------------------------------------------------------------------

public abstract class Resource
{
    [JsonPropertyOrder(-100)]
    public abstract string ResourceType { get; }

    [JsonPropertyOrder(-99)]
    public string? Id { get; set; }

    [JsonPropertyOrder(-98)]
    public Meta? Meta { get; set; }

    public Resource WithProfile(string profile)
    {
        Meta ??= new Meta();
        if (!Meta.Profile.Contains(profile)) Meta.Profile.Add(profile);
        return this;
    }

    /// <summary>"Type/id" relative reference.</summary>
    public string RelativeUrl() => $"{ResourceType}/{Id}";
}

public abstract class DomainResource : Resource
{
    [JsonPropertyOrder(-97)]
    public Narrative? Text { get; set; }

    [JsonPropertyOrder(-96)]
    public List<Resource> Contained { get; set; } = new();

    [JsonPropertyOrder(-95)]
    public List<Extension> Extension { get; set; } = new();

    [JsonPropertyOrder(-94)]
    public List<Extension> ModifierExtension { get; set; } = new();
}

/// <summary>Any resource type this API does not model. Keeps all JSON properties.</summary>
public sealed class GenericResource : Resource
{
    private readonly string _resourceType;
    public GenericResource(string resourceType) { _resourceType = resourceType; }

    [JsonPropertyOrder(-100)]
    public override string ResourceType => _resourceType;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Data { get; set; } = new();
}

// Infrastructure resources ------------------------------------------------------------------

public class Bundle : Resource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Bundle";
    public Identifier? Identifier { get; set; }
    public string? Type { get; set; }   // collection | searchset | history | ...
    public string? Timestamp { get; set; }
    public int? Total { get; set; }
    public List<BundleLink> Link { get; set; } = new();
    public List<BundleEntry> Entry { get; set; } = new();

    public IEnumerable<T> ResourcesOf<T>() where T : Resource =>
        Entry.Select(e => e.Resource).OfType<T>();

    public BundleEntry Add(Resource resource, string? fullUrl = null)
    {
        var entry = new BundleEntry { Resource = resource, FullUrl = fullUrl };
        Entry.Add(entry);
        return entry;
    }
}

public class BundleLink
{
    public string Relation { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class BundleEntry
{
    public string? FullUrl { get; set; }
    public Resource? Resource { get; set; }
    public BundleEntryRequest? Request { get; set; }
}

public class BundleEntryRequest
{
    public string Method { get; set; } = "POST";
    public string Url { get; set; } = string.Empty;
}

public class Parameters : Resource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Parameters";
    public List<ParametersParameter> Parameter { get; set; } = new();

    public IEnumerable<ParametersParameter> Get(string name) =>
        Parameter.Where(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    public ParametersParameter? GetSingle(string name) => Get(name).FirstOrDefault();

    public Parameters AddResource(string name, Resource resource)
    {
        Parameter.Add(new ParametersParameter { Name = name, Resource = resource });
        return this;
    }
}

public class ParametersParameter
{
    public string Name { get; set; } = string.Empty;
    public string? ValueString { get; set; }
    public string? ValueCode { get; set; }
    public bool? ValueBoolean { get; set; }
    public int? ValueInteger { get; set; }
    public string? ValueDate { get; set; }
    public string? ValueDateTime { get; set; }
    public string? ValueInstant { get; set; }
    public string? ValueUri { get; set; }
    public string? ValueUrl { get; set; }
    public string? ValueCanonical { get; set; }
    public Identifier? ValueIdentifier { get; set; }
    public Coding? ValueCoding { get; set; }
    public CodeableConcept? ValueCodeableConcept { get; set; }
    public ResourceReference? ValueReference { get; set; }
    public Resource? Resource { get; set; }
    public List<ParametersParameter> Part { get; set; } = new();
}

public class OperationOutcome : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "OperationOutcome";
    public List<OperationOutcomeIssue> Issue { get; set; } = new();

    public bool HasErrors => Issue.Any(i => i.Severity is "error" or "fatal");

    public static OperationOutcome Create(string severity, string code, string diagnostics, string? expression = null)
    {
        var oo = new OperationOutcome();
        oo.Add(severity, code, diagnostics, expression);
        return oo;
    }

    public OperationOutcome Add(string severity, string code, string diagnostics, string? expression = null)
    {
        var issue = new OperationOutcomeIssue { Severity = severity, Code = code, Diagnostics = diagnostics };
        if (expression != null) issue.Expression.Add(expression);
        Issue.Add(issue);
        return this;
    }
}

public class OperationOutcomeIssue
{
    public string Severity { get; set; } = "error";
    public string Code { get; set; } = "processing";
    public CodeableConcept? Details { get; set; }
    public string? Diagnostics { get; set; }
    public List<string> Expression { get; set; } = new();
}

// Administrative resources ------------------------------------------------------------------

public class Patient : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Patient";
    public List<Identifier> Identifier { get; set; } = new();
    public bool? Active { get; set; }
    public List<HumanName> Name { get; set; } = new();
    public string? Gender { get; set; }
    public string? BirthDate { get; set; }
    public List<Address> Address { get; set; } = new();
}

public class Organization : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Organization";
    public List<Identifier> Identifier { get; set; } = new();
    public bool? Active { get; set; }
    public List<CodeableConcept> Type { get; set; } = new();
    public string? Name { get; set; }
    public List<ContactPoint> Telecom { get; set; } = new();
    public List<Address> Address { get; set; } = new();
}

public class Practitioner : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Practitioner";
    public List<Identifier> Identifier { get; set; } = new();
    public List<HumanName> Name { get; set; } = new();
}

public class PractitionerRole : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "PractitionerRole";
    public List<Identifier> Identifier { get; set; } = new();
    public ResourceReference? Practitioner { get; set; }
    public ResourceReference? Organization { get; set; }
    public List<CodeableConcept> Code { get; set; } = new();
    public List<CodeableConcept> Specialty { get; set; } = new();
}

public class Coverage : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Coverage";
    public List<Identifier> Identifier { get; set; } = new();
    public string Status { get; set; } = "active";
    public CodeableConcept? Type { get; set; }
    public ResourceReference? Subscriber { get; set; }
    public string? SubscriberId { get; set; }
    public ResourceReference? Beneficiary { get; set; }
    public CodeableConcept? Relationship { get; set; }
    public Period? Period { get; set; }
    public List<ResourceReference> Payor { get; set; } = new();
    public List<CoverageClass> Class { get; set; } = new();

    /// <summary>Value of Coverage.class where type = plan (used to pick coverage rules).</summary>
    public string? PlanId() =>
        Class.FirstOrDefault(c => c.Type?.HasCode("plan") == true)?.Value;
}

public class CoverageClass
{
    public CodeableConcept? Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Name { get; set; }
}

// Workflow resources ------------------------------------------------------------------------

public class FhirTask : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Task";
    public List<Identifier> Identifier { get; set; } = new();
    public string Status { get; set; } = "requested";
    public CodeableConcept? StatusReason { get; set; }
    public string Intent { get; set; } = "order";
    public CodeableConcept? Code { get; set; }
    public ResourceReference? Focus { get; set; }
    public ResourceReference? For { get; set; }
    public string? AuthoredOn { get; set; }
    public string? LastModified { get; set; }
    public ResourceReference? Requester { get; set; }
    public ResourceReference? Owner { get; set; }
    public CodeableConcept? ReasonCode { get; set; }
    public ResourceReference? ReasonReference { get; set; }
    public TaskRestriction? Restriction { get; set; }
    public List<TaskParameter> Input { get; set; } = new();
    public List<TaskParameter> Output { get; set; } = new();
}

public class TaskRestriction
{
    public int? Repetitions { get; set; }
    public Period? Period { get; set; }
}

public class TaskParameter : BackboneElement
{
    public CodeableConcept Type { get; set; } = new();
    public string? ValueString { get; set; }
    public string? ValueUrl { get; set; }
    public string? ValueCanonical { get; set; }
    public string? ValueDate { get; set; }
    public bool? ValueBoolean { get; set; }
    public Identifier? ValueIdentifier { get; set; }
    public CodeableConcept? ValueCodeableConcept { get; set; }
    public ResourceReference? ValueReference { get; set; }
}

public class CommunicationRequest : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "CommunicationRequest";
    public List<Identifier> Identifier { get; set; } = new();
    public string Status { get; set; } = "active";
    public List<CodeableConcept> Category { get; set; } = new();
    public ResourceReference? Subject { get; set; }
    public List<CommunicationRequestPayload> Payload { get; set; } = new();
    public string? AuthoredOn { get; set; }
    public ResourceReference? Requester { get; set; }
    public List<ResourceReference> Recipient { get; set; } = new();
    public ResourceReference? Sender { get; set; }
}

public class CommunicationRequestPayload : BackboneElement
{
    public string? ContentString { get; set; }
    public ResourceReference? ContentReference { get; set; }
}

public class DocumentReference : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "DocumentReference";
    public List<Identifier> Identifier { get; set; } = new();
    public string Status { get; set; } = "current";
    public CodeableConcept? Type { get; set; }
    public List<CodeableConcept> Category { get; set; } = new();
    public ResourceReference? Subject { get; set; }
    public string? Date { get; set; }
    public List<ResourceReference> Author { get; set; } = new();
    public string? Description { get; set; }
    public List<DocumentReferenceContent> Content { get; set; } = new();
}

public class DocumentReferenceContent
{
    public Attachment Attachment { get; set; } = new();
}

/// <summary>R4 Subscription using the Subscriptions R5 Backport IG (as required by PAS).</summary>
public class Subscription : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Subscription";
    public string Status { get; set; } = "requested";
    public string? End { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>The SubscriptionTopic canonical URL.</summary>
    public string Criteria { get; set; } = string.Empty;

    /// <summary>Holds the backport-filter-criteria extension.</summary>
    [JsonPropertyName("_criteria")]
    public PrimitiveExtensions? CriteriaExtensions { get; set; }

    public string? Error { get; set; }
    public SubscriptionChannel Channel { get; set; } = new();
}

public class SubscriptionChannel
{
    public List<Extension> Extension { get; set; } = new();
    public string Type { get; set; } = "rest-hook";
    public string? Endpoint { get; set; }
    public string? Payload { get; set; }

    /// <summary>Holds the backport-payload-content extension (id-only | full-resource).</summary>
    [JsonPropertyName("_payload")]
    public PrimitiveExtensions? PayloadExtensions { get; set; }

    public List<string> Header { get; set; } = new();
}
