namespace DaVinci.PriorAuth.Constants;

/// <summary>CDS Hooks supported by CRD 2.0.1. Primary hooks SHALL return Coverage Information.</summary>
public static class CrdHooks
{
    public const string AppointmentBook = "appointment-book";   // primary
    public const string OrderSign = "order-sign";               // primary
    public const string OrderDispatch = "order-dispatch";       // primary
    public const string OrderSelect = "order-select";           // secondary
    public const string EncounterStart = "encounter-start";     // secondary
    public const string EncounterDischarge = "encounter-discharge"; // secondary

    public static readonly string[] All =
        { AppointmentBook, OrderSign, OrderDispatch, OrderSelect, EncounterStart, EncounterDischarge };

    /// <summary>CDS service id used in the URL: POST /cds-services/{id}</summary>
    public static string ServiceId(string hook) => $"crd-{hook}";
}

public static class CrdExtensionUrls
{
    public const string CoverageInformation =
        "http://hl7.org/fhir/us/davinci-crd/StructureDefinition/ext-coverage-information";

    /// <summary>Discovery-response extension that lists the options the service supports.</summary>
    public const string ConfigurationOptions = "davinci-crd.configuration-options";

    /// <summary>Hook-request extension in which the EHR passes the chosen option values.</summary>
    public const string Configuration = "davinci-crd.configuration";
}

/// <summary>Sub-extension names inside ext-coverage-information (CRD 2.0.1).</summary>
public static class CoverageInfoParts
{
    public const string Coverage = "coverage";                       // 1..1 Reference(Coverage)
    public const string Covered = "covered";                         // 1..1 code
    public const string PaNeeded = "pa-needed";                      // 0..1 code
    public const string DocNeeded = "doc-needed";                    // 0..1 code
    public const string DocPurpose = "doc-purpose";                  // 0..* code
    public const string InfoNeeded = "info-needed";                  // 0..* code
    public const string BillingCode = "billingCode";                 // 0..* Coding
    public const string Reason = "reason";                           // 0..* CodeableConcept
    public const string Detail = "detail";                           // 0..* complex (category + value[x])
    public const string Dependency = "dependency";                   // 0..* Reference
    public const string Questionnaire = "questionnaire";             // 0..* canonical
    public const string Response = "response";                       // 0..* Reference(QuestionnaireResponse)
    public const string Date = "date";                               // 1..1 date
    public const string CoverageAssertionId = "coverage-assertion-id"; // 1..1 string
    public const string SatisfiedPaId = "satisfied-pa-id";           // 0..1 string
    public const string Contact = "contact";                         // 0..* ContactPoint

    public const string DetailCategory = "category";
    public const string DetailValue = "value";
}

public static class CoveredCodes
{
    public const string Covered = "covered";
    public const string NotCovered = "not-covered";
    public const string Conditional = "conditional";
}

public static class PaNeededCodes
{
    public const string NoAuth = "no-auth";
    public const string AuthNeeded = "auth-needed";
    public const string Satisfied = "satisfied";
    public const string PerformPa = "performpa";
    public const string Conditional = "conditional";
}

public static class DocNeededCodes
{
    public const string Clinical = "clinical";
    public const string Admin = "admin";
    public const string Both = "both";
    public const string Conditional = "conditional";
}

public static class DocPurposeCodes
{
    public const string WithPa = "withpa";
    public const string WithClaim = "withclaim";
    public const string WithOrder = "withorder";
    public const string RetainDoc = "retain-doc";
}

public static class InfoNeededCodes
{
    public const string Performer = "performer";
    public const string Location = "location";
    public const string Timeframe = "timeframe";
    public const string ContractWindow = "contract-window";
}

/// <summary>Codes for the 'detail.category' sub-extension (CRD temp code system).</summary>
public static class CoverageDetailCodes
{
    public const string DetailCode = "detail-code";
    public const string AllowedQuantity = "allowed-quantity";
    public const string AllowedPeriod = "allowed-period";
    public const string InNetworkCopay = "in-network-copay";
    public const string OutNetworkCopay = "out-network-copay";
    public const string AuthOutNetworkOnly = "auth-out-network-only";
    public const string ConcurrentReview = "concurrent-review";
    public const string AppropriateUseNeeded = "appropriate-use-needed";
}

public static class CrdReasonCodes
{
    public const string GoldCard = "gold-card";
}

/// <summary>CRD card types (also the configuration option codes).</summary>
public static class CrdCardTypes
{
    public const string CoverageInfo = "coverage-info";
    public const string Claim = "claim";
    public const string Insurance = "insurance";
    public const string Limits = "limits";
    public const string Network = "network";
    public const string AppropriateUse = "appropriate-use";
    public const string Cost = "cost";
    public const string TherapyAlternativesOpt = "therapy-alternatives-opt";
    public const string TherapyAlternativesReq = "therapy-alternatives-req";
    public const string ClinicalReminder = "clinical-reminder";
    public const string DuplicateTherapy = "duplicate-therapy";
    public const string Contraindication = "contraindication";
    public const string Guideline = "guideline";
    public const string OffGuideline = "off-guideline";

    /// <summary>Integer option limiting how many cards are returned.</summary>
    public const string MaxCards = "max-cards";
}

public static class CdsIndicators
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public static class CdsLinkTypes
{
    public const string Absolute = "absolute";
    public const string Smart = "smart";
}

public static class CdsActionTypes
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
}

/// <summary>Prefetch keys advertised in the discovery document.</summary>
public static class CrdPrefetchKeys
{
    public const string Patient = "patient";
    public const string Coverage = "coverage";
    public const string ServiceRequest = "serviceRequestBundle";
    public const string DeviceRequest = "deviceRequestBundle";
    public const string MedicationRequest = "medicationRequestBundle";
    public const string Appointment = "appointmentBundle";
    public const string Encounter = "encounterBundle";
    public const string Order = "order";
}

/// <summary>Order / request resource types CRD evaluates.</summary>
public static class CrdOrderTypes
{
    public const string ServiceRequest = "ServiceRequest";
    public const string DeviceRequest = "DeviceRequest";
    public const string MedicationRequest = "MedicationRequest";
    public const string NutritionOrder = "NutritionOrder";
    public const string VisionPrescription = "VisionPrescription";
    public const string CommunicationRequest = "CommunicationRequest";
    public const string Appointment = "Appointment";
    public const string Encounter = "Encounter";
}
