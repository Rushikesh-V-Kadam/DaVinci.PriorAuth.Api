namespace DaVinci.PriorAuth.Infrastructure;

/// <summary>Bound from appsettings.json section "Payer".</summary>
public class PayerOptions
{
    public const string Section = "Payer";

    /// <summary>Public base URL of this API, e.g. https://payer.example.org (no trailing slash).</summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";

    public string PayerName { get; set; } = "Example Health Plan";
    public string PayerIdentifierSystem { get; set; } = "http://example.org/payer-ids";
    public string PayerIdentifierValue { get; set; } = "PAYER001";

    /// <summary>System used for member ids (Coverage.subscriberId / Patient.identifier).</summary>
    public string MemberIdSystem { get; set; } = "http://example.org/member-ids";

    /// <summary>System for payer-assigned tracking / authorization numbers.</summary>
    public string AuthNumberSystem { get; set; } = "http://example.org/prior-auth-numbers";
    public string TrackingIdSystem { get; set; } = "http://example.org/cdex/tracking-ids";

    /// <summary>SMART launch URL of the DTR app shown on the CRD card (type=smart).</summary>
    public string DtrAppLaunchUrl { get; set; } = "https://dtr.example.org/launch";

    /// <summary>Name of the output bundle parameter of $questionnaire-package: PackageBundle (profile) or return (OperationDefinition).</summary>
    public string PackageOutputParameterName { get; set; } = "PackageBundle";

    /// <summary>Standard / expedited decision windows (CMS-0057-F: 7 calendar days / 72 hours).</summary>
    public int StandardDecisionHours { get; set; } = 168;
    public int ExpeditedDecisionHours { get; set; } = 72;

    /// <summary>How long an approved authorization stays valid when the request gives no service period.</summary>
    public int DefaultAuthValidityDays { get; set; } = 180;

    /// <summary>Days the provider has to answer a CDex attachment request.</summary>
    public int AttachmentDueDays { get; set; } = 14;

    /// <summary>If set, CDex Tasks are also POSTed to this provider FHIR endpoint (push model).</summary>
    public string? ProviderTaskEndpoint { get; set; }

    /// <summary>Turn off to run locally without tokens. NEVER disable in production.</summary>
    public bool RequireAuthentication { get; set; } = false;
}
