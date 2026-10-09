using System.Text.Json.Serialization;

namespace DaVinci.PriorAuth.Models.Fhir;

/// <summary>FHIR R4 Claim, with the elements used by the PAS Claim / Claim Update / Claim Inquiry profiles.</summary>
public class Claim : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Claim";
    public List<Identifier> Identifier { get; set; } = new();
    public string? Status { get; set; }          // PAS: fixed "active"
    public CodeableConcept? Type { get; set; }
    public CodeableConcept? SubType { get; set; }
    public string? Use { get; set; }             // PAS: fixed "preauthorization"
    public ResourceReference? Patient { get; set; }
    public Period? BillablePeriod { get; set; }
    public string? Created { get; set; }
    public ResourceReference? Enterer { get; set; }
    public ResourceReference? Insurer { get; set; }
    public ResourceReference? Provider { get; set; }
    public CodeableConcept? Priority { get; set; }
    public List<ClaimRelated> Related { get; set; } = new();
    public ResourceReference? Referral { get; set; }
    public ResourceReference? Facility { get; set; }
    public List<ClaimCareTeam> CareTeam { get; set; } = new();
    public List<ClaimSupportingInfo> SupportingInfo { get; set; } = new();
    public List<ClaimDiagnosis> Diagnosis { get; set; } = new();
    public List<ClaimInsurance> Insurance { get; set; } = new();
    public List<ClaimItem> Item { get; set; } = new();
    public Money? Total { get; set; }
}

public class ClaimRelated : BackboneElement
{
    public ResourceReference? Claim { get; set; }
    public CodeableConcept? Relationship { get; set; }
    public Identifier? Reference { get; set; }
}

public class ClaimCareTeam : BackboneElement
{
    public int Sequence { get; set; }
    public ResourceReference? Provider { get; set; }
    public bool? Responsible { get; set; }
    public CodeableConcept? Role { get; set; }
    public CodeableConcept? Qualification { get; set; }
}

public class ClaimSupportingInfo : BackboneElement
{
    public int Sequence { get; set; }
    public CodeableConcept Category { get; set; } = new();
    public CodeableConcept? Code { get; set; }
    public string? TimingDate { get; set; }
    public Period? TimingPeriod { get; set; }
    public bool? ValueBoolean { get; set; }
    public string? ValueString { get; set; }
    public Quantity? ValueQuantity { get; set; }
    public Attachment? ValueAttachment { get; set; }
    public ResourceReference? ValueReference { get; set; }
    public CodeableConcept? Reason { get; set; }
}

public class ClaimDiagnosis : BackboneElement
{
    public int Sequence { get; set; }
    public CodeableConcept? DiagnosisCodeableConcept { get; set; }
    public ResourceReference? DiagnosisReference { get; set; }
    public List<CodeableConcept> Type { get; set; } = new();
}

public class ClaimInsurance : BackboneElement
{
    public int Sequence { get; set; }
    public bool Focal { get; set; }
    public Identifier? Identifier { get; set; }
    public ResourceReference? Coverage { get; set; }
    public List<string> PreAuthRef { get; set; } = new();
}

public class ClaimItem : BackboneElement
{
    public int Sequence { get; set; }
    public List<int> CareTeamSequence { get; set; } = new();
    public List<int> DiagnosisSequence { get; set; } = new();
    public List<int> InformationSequence { get; set; } = new();
    public CodeableConcept? Revenue { get; set; }
    public CodeableConcept? Category { get; set; }
    public CodeableConcept? ProductOrService { get; set; }
    public List<CodeableConcept> Modifier { get; set; } = new();
    public string? ServicedDate { get; set; }
    public Period? ServicedPeriod { get; set; }
    public CodeableConcept? LocationCodeableConcept { get; set; }
    public Quantity? Quantity { get; set; }
    public Money? UnitPrice { get; set; }
    public Money? Net { get; set; }
}

/// <summary>FHIR R4 ClaimResponse, with the elements used by the PAS ClaimResponse profile.</summary>
public class ClaimResponse : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "ClaimResponse";
    public List<Identifier> Identifier { get; set; } = new();
    public string Status { get; set; } = "active";
    public CodeableConcept? Type { get; set; }
    public string Use { get; set; } = "preauthorization";
    public ResourceReference? Patient { get; set; }
    public string? Created { get; set; }
    public ResourceReference? Insurer { get; set; }
    public ResourceReference? Requestor { get; set; }
    public ResourceReference? Request { get; set; }
    public string Outcome { get; set; } = "complete";
    public string? Disposition { get; set; }
    public string? PreAuthRef { get; set; }
    public Period? PreAuthPeriod { get; set; }
    public List<ClaimResponseItem> Item { get; set; } = new();
    public List<ClaimResponseProcessNote> ProcessNote { get; set; } = new();
    public List<ResourceReference> CommunicationRequest { get; set; } = new();
    public List<ClaimResponseError> Error { get; set; } = new();
}

public class ClaimResponseItem : BackboneElement
{
    public int ItemSequence { get; set; }
    public List<int> NoteNumber { get; set; } = new();
    public List<ClaimResponseAdjudication> Adjudication { get; set; } = new();
}

/// <summary>ClaimResponse.item.adjudication. PAS 2.0.1 puts the reviewAction extension (the line decision) here.</summary>
public class ClaimResponseAdjudication : BackboneElement
{
    public CodeableConcept Category { get; set; } = new();
    public CodeableConcept? Reason { get; set; }
    public Money? Amount { get; set; }
    public decimal? Value { get; set; }
}

public class ClaimResponseProcessNote
{
    public int? Number { get; set; }
    public string? Type { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class ClaimResponseError : BackboneElement
{
    public int? ItemSequence { get; set; }
    public CodeableConcept Code { get; set; } = new();
}
