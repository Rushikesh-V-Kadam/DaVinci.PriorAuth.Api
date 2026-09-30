using System.Text.Json.Nodes;
using DaVinci.PriorAuth.Models.Fhir;

namespace DaVinci.PriorAuth.Models.Domain;

// ============================================================ Coverage rules (drive CRD, DTR and PAS)

/// <summary>
/// One row of the payer's coverage policy table. Keyed by plan + billing code.
/// This is the single source of truth: CRD answers from it, DTR picks questionnaires
/// from it, and PAS adjudicates against it.
/// </summary>
public class CoverageRule
{
    public string RuleId { get; set; } = string.Empty;
    public string PlanId { get; set; } = "*";                 // Coverage.class[plan].value, "*" = all plans
    public string CodeSystem { get; set; } = string.Empty;    // CPT / HCPCS / RxNorm ...
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // ---- CRD outputs
    public string Covered { get; set; } = "covered";          // covered | not-covered | conditional
    public string PaNeeded { get; set; } = "no-auth";         // no-auth | auth-needed | performpa | conditional
    public string? DocNeeded { get; set; }                    // null | clinical | admin | both | conditional
    public List<string> DocPurpose { get; set; } = new();     // withpa | withclaim | withorder | retain-doc
    public List<string> QuestionnaireUrls { get; set; } = new();

    /// <summary>Code is too generic (e.g. an unlisted-procedure code): answer 'conditional' + detail-code.</summary>
    public bool RequiresMoreSpecificCode { get; set; }

    /// <summary>PA depends on where the service happens; if the order has no location -> info-needed=location.</summary>
    public List<string> PaRequiredPlacesOfService { get; set; } = new();

    /// <summary>NPIs of gold-carded ordering practitioners (PA waived).</summary>
    public List<string> GoldCardNpis { get; set; } = new();

    public int? AllowedQuantity { get; set; }
    public int? AllowedPeriodMonths { get; set; }
    public string? NotCoveredReason { get; set; }
    public string? PolicyUrl { get; set; }

    // ---- PAS adjudication
    /// <summary>LOINC attachment codes that must accompany the PA (requested via CDex Task if missing).</summary>
    public List<string> RequiredAttachmentLoinc { get; set; } = new();

    /// <summary>Automatic approval criteria evaluated against the DTR QuestionnaireResponse.</summary>
    public List<ApprovalCriterion> ApprovalCriteria { get; set; } = new();

    public string X12ServiceType { get; set; } = "1";
}

/// <summary>A single automatic-approval check against a QuestionnaireResponse answer.</summary>
public class ApprovalCriterion
{
    public string LinkId { get; set; } = string.Empty;
    public string Operator { get; set; } = ">=";   // >=, >, <=, <, =, true
    public decimal? Number { get; set; }
    public string Description { get; set; } = string.Empty;
}

// ============================================================ CRD evaluation

/// <summary>An order/appointment/encounter extracted from a hook, kept as raw JSON for echo.</summary>
public class OrderContext
{
    public string ResourceType { get; set; } = string.Empty;
    public string? Id { get; set; }
    public JsonObject Raw { get; set; } = new();
    public List<Coding> Codes { get; set; } = new();
    public decimal? Quantity { get; set; }
    public string? PlaceOfServiceCode { get; set; }
    public string? RequesterNpi { get; set; }
    public bool HasPerformer { get; set; }
    public string Reference => $"{ResourceType}/{Id}";
}

/// <summary>Result of evaluating one order against the rules. Mapped 1:1 into ext-coverage-information.</summary>
public class CoverageDecision
{
    public string Covered { get; set; } = "covered";
    public string? PaNeeded { get; set; }
    public string? DocNeeded { get; set; }
    public List<string> DocPurpose { get; set; } = new();
    public List<string> InfoNeeded { get; set; } = new();
    public List<Coding> BillingCodes { get; set; } = new();
    public List<CodeableConcept> Reasons { get; set; } = new();
    public List<CoverageDetail> Details { get; set; } = new();
    public List<string> QuestionnaireUrls { get; set; } = new();
    public string CoverageAssertionId { get; set; } = Guid.NewGuid().ToString("N");
    public string? SatisfiedPaId { get; set; }
    public string Date { get; set; } = FhirDate.Today();
    public string? RuleId { get; set; }
    public string? PolicyUrl { get; set; }
    public string Summary { get; set; } = string.Empty;

    public bool PriorAuthRequired => PaNeeded is "auth-needed" or "performpa";
    public bool DocumentationRequired => DocNeeded != null;
}

public class CoverageDetail
{
    public string Category { get; set; } = string.Empty;   // CoverageDetailCodes
    public bool? ValueBoolean { get; set; }
    public Quantity? ValueQuantity { get; set; }
    public Period? ValuePeriod { get; set; }
    public string? ValueString { get; set; }
}

/// <summary>Stored CRD answer, looked up later by DTR (context = coverage-assertion-id) and PAS.</summary>
public class CoverageAssertion
{
    public string CoverageAssertionId { get; set; } = string.Empty;
    public string PatientMemberId { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public string? BillingCode { get; set; }
    public CoverageDecision Decision { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ============================================================ PAS

public enum PriorAuthStatus { Pended, Complete, Cancelled, Replaced, Error }

public class PriorAuthRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BundleIdentifierKey { get; set; } = string.Empty;   // Bundle.identifier system|value (idempotency)
    public string ClaimIdentifierKey { get; set; } = string.Empty;    // Claim.identifier system|value
    public Identifier ClaimIdentifier { get; set; } = new();
    public string PatientMemberId { get; set; } = string.Empty;
    public string? RequestorOrgIdentifierKey { get; set; }            // used by subscriptions (orgIdentifier)
    public string? ProviderNpi { get; set; }
    public string Priority { get; set; } = "normal";
    public PriorAuthStatus Status { get; set; } = PriorAuthStatus.Pended;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueAt { get; set; }
    public string? PreAuthRef { get; set; }
    public string TrackingId { get; set; } = string.Empty;           // links CDex attachments to this request
    public List<PriorAuthItemRecord> Items { get; set; } = new();
    public Bundle RequestBundle { get; set; } = new();
    public Bundle? LatestResponseBundle { get; set; }
    public List<string> CdexTaskIds { get; set; } = new();
    public List<ReceivedAttachment> Attachments { get; set; } = new();
    public string? ReplacedByRecordId { get; set; }
    public int Version { get; set; } = 1;
}

public class PriorAuthItemRecord
{
    public int Sequence { get; set; }
    public string? CodeSystem { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }
    public decimal RequestedQuantity { get; set; } = 1;
    public decimal? AuthorizedQuantity { get; set; }
    public string ReviewAction { get; set; } = "A4";         // X12 306
    public string? ReasonCode { get; set; }                   // X12 886 (licensed code list)
    public string? ReasonText { get; set; }
    public string? AuthNumber { get; set; }
    public Period? AuthorizedPeriod { get; set; }
    public string? RuleId { get; set; }
    public List<string> MissingAttachments { get; set; } = new();
    public List<string> MissingQuestionnaires { get; set; } = new();
    public bool Cancelled { get; set; }
}

public class ReceivedAttachment
{
    public string? LoincCode { get; set; }
    public List<int> LineItems { get; set; } = new();
    public string ContentResourceType { get; set; } = string.Empty;
    public Resource? Content { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Result of one adjudication run.</summary>
public class AdjudicationResult
{
    public List<PriorAuthItemRecord> Items { get; set; } = new();
    public bool AnyPended => Items.Any(i => i.ReviewAction == "A4");
    public List<string> AttachmentsNeeded { get; set; } = new();
    public List<(int Line, string Canonical)> QuestionnairesNeeded { get; set; } = new();
}

// ============================================================ Subscriptions

public class SubscriptionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Subscription Resource { get; set; } = new();
    public string OrgIdentifierFilter { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string PayloadContent { get; set; } = "id-only";
    public List<string> Headers { get; set; } = new();
    public long EventCount { get; set; }
}
