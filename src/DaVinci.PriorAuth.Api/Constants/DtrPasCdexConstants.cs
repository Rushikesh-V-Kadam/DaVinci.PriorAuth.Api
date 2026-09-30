namespace DaVinci.PriorAuth.Constants;

// ------------------------------------------------------------------ DTR 2.0.1
public static class DtrOperations
{
    public const string QuestionnairePackage = "$questionnaire-package";
    public const string NextQuestion = "$next-question";
    public const string QuestionnairePackageCanonical =
        "http://hl7.org/fhir/us/davinci-dtr/OperationDefinition/questionnaire-package";
}

/// <summary>$questionnaire-package parameter names (DTR 2.0.1 OperationDefinition).</summary>
public static class DtrParams
{
    public const string Coverage = "coverage";          // in 0..* Coverage
    public const string Order = "order";                // in 0..* Resource
    public const string Referenced = "referenced";      // in 0..* Resource
    public const string Questionnaire = "questionnaire"; // in 0..* canonical
    public const string Context = "context";            // in 0..1 string (coverage-assertion-id or CDex tracking id)
    public const string ChangedSince = "changedsince";  // in 0..1 dateTime

    /// <summary>
    /// Output bundle parameter. The 2.0.1 OperationDefinition names it "return"; the
    /// dtr-qpackage-output-parameters profile and its example use "PackageBundle".
    /// The name is configurable (Dtr:PackageOutputParameterName), default PackageBundle.
    /// </summary>
    public const string PackageBundle = "PackageBundle";
    public const string Return = "return";
    public const string Outcome = "outcome";
}

public static class DtrProfiles
{
    public const string StdQuestionnaire = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-std-questionnaire";
    public const string AdaptiveQuestionnaire = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-questionnaire-adapt";
    public const string AdaptiveQuestionnaireSearch = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-questionnaire-adapt-search";
    public const string QuestionnaireResponse = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-questionnaireresponse";
    public const string QPackageInputParameters = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-qpackage-input-parameters";
    public const string QPackageOutputParameters = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-qpackage-output-parameters";
    public const string QPackageBundle = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/DTR-QPackageBundle";
    public const string QRBundle = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/DTR-QRBundle";
}

public static class DtrExtensionUrls
{
    public const string CqfLibrary = "http://hl7.org/fhir/StructureDefinition/cqf-library";
    public const string InitialExpression = "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression";
    public const string CandidateExpression = "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-candidateExpression";
    public const string CalculatedExpression = "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-calculatedExpression";
    public const string QuestionnaireAdaptive = "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-questionnaireAdaptive";
    public const string InformationOrigin = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/information-origin";
    public const string IntendedUse = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/intendedUse";
    public const string QrContext = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/qr-context";
    public const string AlternativeExpression = "http://hl7.org/fhir/us/ecr/StructureDefinition/us-ph-alternative-expression-extension";
}

public static class InformationOriginCodes
{
    public const string Auto = "auto";
    public const string Override = "override";
    public const string Manual = "manual";
}

// ------------------------------------------------------------------ PAS 2.0.1
public static class PasOperations
{
    public const string Submit = "$submit";
    public const string Inquire = "$inquire";
    public const string SubmitCanonical = "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit";
    public const string InquireCanonical = "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-inquiry";
    public const string ParamResource = "resource"; // in 1..1 Bundle
    public const string ParamReturn = "return";     // out 1..1 Bundle
}

public static class PasProfiles
{
    private const string Base = "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/";
    public const string RequestBundle = Base + "profile-pas-request-bundle";
    public const string ResponseBundle = Base + "profile-pas-response-bundle";
    public const string InquiryRequestBundle = Base + "profile-pas-inquiry-request-bundle";
    public const string InquiryResponseBundle = Base + "profile-pas-inquiry-response-bundle";
    public const string Claim = Base + "profile-claim";
    public const string ClaimUpdate = Base + "profile-claim-update";
    public const string ClaimInquiry = Base + "profile-claim-inquiry";
    public const string ClaimResponse = Base + "profile-claimresponse";
    public const string Task = Base + "profile-task";
    public const string CommunicationRequest = Base + "profile-communicationrequest";
    public const string Subscription = Base + "profile-subscription";
}

public static class PasExtensionUrls
{
    private const string Base = "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/";
    // Claim / Claim.item
    public const string CertificationType = Base + "extension-certificationType";
    public const string ServiceItemRequestType = Base + "extension-serviceItemRequestType";
    public const string LevelOfServiceCode = Base + "extension-levelOfServiceCode";
    public const string RequestedService = Base + "extension-requestedService";
    public const string CareTeamClaimScope = Base + "extension-careTeamClaimScope";
    public const string InfoChanged = Base + "extension-infoChanged";
    public const string InfoCancelled = Base + "modifierextension-infoCancelled";
    public const string ServiceLineNumber = Base + "extension-serviceLineNumber";
    public const string PaLineNumber = Base + "extension-paLineNumber";
    // ClaimResponse.item
    public const string ItemTraceNumber = Base + "extension-itemTraceNumber";
    public const string ItemPreAuthIssueDate = Base + "extension-itemPreAuthIssueDate";
    public const string ItemPreAuthPeriod = Base + "extension-itemPreAuthPeriod";
    public const string AuthorizationNumber = Base + "extension-authorizationNumber";
    public const string AdministrationReferenceNumber = Base + "extension-administrationReferenceNumber";
    public const string ItemRequestedServiceDate = Base + "extension-itemRequestedServiceDate";
    public const string ItemAuthorizedDetail = Base + "extension-itemAuthorizedDetail";
    public const string ItemAuthorizedProvider = Base + "extension-itemAuthorizedProvider";
    public const string ReviewAction = Base + "extension-reviewAction";
    public const string ReviewActionCode = Base + "extension-reviewActionCode";
    // ClaimResponse.error
    public const string ErrorFollowupAction = Base + "extension-errorFollowupAction";
    public const string ErrorElement = Base + "extension-errorElement";
    public const string ErrorPath = Base + "extension-errorPath";
}

/// <summary>Sub-extension names inside extension-reviewAction.</summary>
public static class ReviewActionParts
{
    // the 'code' slice uses the full URL PasExtensionUrls.ReviewActionCode
    public const string Number = "number";
    public const string ReasonCode = "reasonCode";
    public const string SecondSurgicalOpinionFlag = "secondSurgicalOpinionFlag";
}

/// <summary>X12 306 Action Codes (HCR01) used in reviewActionCode.</summary>
public static class X12ReviewActionCodes
{
    public const string CertifiedInTotal = "A1";
    public const string CertifiedPartial = "A2";
    public const string NotCertified = "A3";
    public const string Pended = "A4";
    public const string Modified = "A6";
    public const string Cancelled = "C";
    public const string ContactPayer = "CT";
    public const string NoActionRequired = "NA";

    public static string Display(string code) => code switch
    {
        CertifiedInTotal => "Certified in total",
        CertifiedPartial => "Certified - partial",
        NotCertified => "Not Certified",
        Pended => "Pended",
        Modified => "Modified",
        Cancelled => "Cancelled",
        ContactPayer => "Contact Payer",
        NoActionRequired => "No Action Required",
        _ => code
    };

    public static bool IsFinal(string code) => code != Pended;
}

/// <summary>X12 1322 Certification Type Codes (UM02).</summary>
public static class X12CertificationTypes
{
    public const string AppealImmediate = "1";
    public const string AppealStandard = "2";
    public const string Cancel = "3";
    public const string Extension = "4";
    public const string Initial = "I";
    public const string Renewal = "R";
    public const string Revised = "S";
}

/// <summary>X12 1525 Request Category Codes (UM01).</summary>
public static class X12RequestTypes
{
    public const string AdmissionReview = "AR";
    public const string HealthServicesReview = "HS";
    public const string SpecialtyCareReview = "SC";
}

/// <summary>X12 1365 Service Type Codes used for Claim.item.category (subset).</summary>
public static class X12ServiceTypes
{
    public const string MedicalCare = "1";
    public const string Surgical = "2";
    public const string Consultation = "3";
    public const string DiagnosticXRay = "4";
    public const string DmePurchase = "12";
    public const string DmeRental = "18";
    public const string HomeHealthCare = "42";
    public const string PhysicalTherapy = "PT";
    public const string Dialysis = "76";
}

/// <summary>PAS temporary codes (PASTempCodes).</summary>
public static class PasTempCodes
{
    // supportingInfo categories
    public const string PatientEvent = "patientEvent";
    public const string AdmissionDates = "admissionDates";
    public const string DischargeDates = "dischargeDates";
    public const string AdditionalInformation = "additionalInformation";
    public const string FreeFormMessage = "freeFormMessage";
    // PAS Task
    public const string AttachmentRequestCode = "attachment-request-code";
    public const string AttachmentRequestQuestionnaire = "attachment-request-questionnaire";
    public const string PriorAuthorization = "priorAuthorization";
    public const string PayerUrl = "payer-url";
    public const string AttachmentsNeeded = "attachments-needed";
    public const string QuestionnairesNeeded = "questionnaires-needed";
    // infoChanged
    public const string Added = "added";
    public const string Changed = "changed";
}

/// <summary>ClaimResponse.outcome (remittance-outcome) values.</summary>
public static class ClaimOutcomes
{
    public const string Queued = "queued";
    public const string Complete = "complete";
    public const string Error = "error";
    public const string Partial = "partial";
}

public static class PasSubscription
{
    public const string TopicCanonical = "http://hl7.org/fhir/us/davinci-pas/SubscriptionTopic/PASSubscriptionTopic";
    public const string FilterParameter = "orgIdentifier";
    public const string BackportFilterCriteria = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-filter-criteria";
    public const string BackportPayloadContent = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-payload-content";
    public const string BackportStatusProfile = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-subscription-status-r4";
    public const string BackportNotificationProfile = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-subscription-notification-r4";
    public const string PayloadIdOnly = "id-only";
    public const string PayloadFullResource = "full-resource";
}

// ------------------------------------------------------------------ CDex 2.0.0
public static class CdexOperations
{
    public const string SubmitAttachment = "$submit-attachment";
    public const string SubmitAttachmentCanonical = "http://hl7.org/fhir/us/davinci-cdex/OperationDefinition/submit-attachment";
}

/// <summary>$submit-attachment parameter names (CDex 2.0.0).</summary>
public static class CdexParams
{
    public const string TrackingId = "TrackingId";         // 1..1 Identifier
    public const string AttachTo = "AttachTo";             // 1..1 code: claim | preauthorization
    public const string PayerId = "PayerId";               // 0..1 Identifier
    public const string OrganizationId = "OrganizationId"; // 0..1 Identifier
    public const string ProviderId = "ProviderId";         // 0..1 Identifier
    public const string MemberId = "MemberId";             // 1..1 Identifier
    public const string ServiceDate = "ServiceDate";       // 0..1 dateTime
    public const string Attachment = "Attachment";         // 1..* part
    public const string LineItem = "LineItem";             // part 0..* string
    public const string Code = "Code";                     // part 0..1 CodeableConcept (LOINC)
    public const string Content = "Content";               // part 1..1 Resource
    public const string Final = "Final";                   // 0..1 boolean (default true)
}

public static class CdexAttachTo
{
    public const string Claim = "claim";
    public const string PreAuthorization = "preauthorization";
}

public static class CdexProfiles
{
    public const string TaskAttachmentRequest = "http://hl7.org/fhir/us/davinci-cdex/StructureDefinition/cdex-task-attachment-request";
    public const string ParametersSubmitAttachment = "http://hl7.org/fhir/us/davinci-cdex/StructureDefinition/cdex-parameters-submit-attachment";
}

public static class CdexTempCodes
{
    public const string AttachmentRequestCode = "attachment-request-code";
    public const string AttachmentRequestQuestionnaire = "attachment-request-questionnaire";
    public const string TrackingId = "tracking-id";
    public const string PayerUrl = "payer-url";
    public const string ServiceDate = "service-date";
    public const string SignatureFlag = "signature-flag";
    public const string PurposeOfUse = "purpose-of-use";
    public const string MultipleSubmitsFlag = "multiple-submits-flag";
}

public static class TaskStatusCodes
{
    public const string Draft = "draft";
    public const string Requested = "requested";
    public const string Received = "received";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string InProgress = "in-progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>LOINC codes used in the seed data for attachment requests.</summary>
public static class LoincCodes
{
    public const string ProgressNote = "11506-3";
    public const string DiagnosticImagingStudy = "18748-4";
    public const string LaboratoryReport = "11502-2";
    public const string PayerQuestionnaire = "102089-0"; // "complete payer questionnaire" per PAS additional-info guidance
}
