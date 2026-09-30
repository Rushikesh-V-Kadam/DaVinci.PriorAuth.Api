namespace DaVinci.PriorAuth.Constants;

/// <summary>Implementation Guide versions this API conforms to (CMS-0057-F named versions).</summary>
public static class IgVersions
{
    public const string FhirVersion = "4.0.1";
    public const string UsCore = "3.1.1";
    public const string Crd = "2.0.1";
    public const string Dtr = "2.0.1";
    public const string Pas = "2.0.1";
    public const string Cdex = "2.0.0";
}

public static class MediaTypes
{
    public const string FhirJson = "application/fhir+json";
    public const string Json = "application/json";
    public const string Cql = "text/cql";
    public const string ElmJson = "application/elm+json";
}

/// <summary>Code system URIs used across CRD, DTR, PAS and CDex.</summary>
public static class CodeSystems
{
    // Da Vinci temporary code systems
    public const string CrdTemp = "http://hl7.org/fhir/us/davinci-crd/CodeSystem/temp";
    public const string PasTemp = "http://hl7.org/fhir/us/davinci-pas/CodeSystem/PASTempCodes";
    public const string CdexTemp = "http://hl7.org/fhir/us/davinci-cdex/CodeSystem/cdex-temp";
    public const string HrexTemp = "http://hl7.org/fhir/us/davinci-hrex/CodeSystem/hrex-temp";
    public const string SdcTemp = "http://hl7.org/fhir/uv/sdc/CodeSystem/temp";

    // HL7 terminology
    public const string ClaimType = "http://terminology.hl7.org/CodeSystem/claim-type";
    public const string ProcessPriority = "http://terminology.hl7.org/CodeSystem/processpriority";
    public const string Adjudication = "http://terminology.hl7.org/CodeSystem/adjudication";
    public const string ClaimUse = "http://hl7.org/fhir/claim-use";
    public const string ActReason = "http://terminology.hl7.org/CodeSystem/v3-ActReason";
    public const string CoverageClass = "http://terminology.hl7.org/CodeSystem/coverage-class";
    public const string SubscriberRelationship = "http://terminology.hl7.org/CodeSystem/subscriber-relationship";
    public const string LibraryType = "http://terminology.hl7.org/CodeSystem/library-type";

    // X12 code systems used by PAS
    public const string X12ReviewAction306 = "https://codesystem.x12.org/005010/306";
    public const string X12ReviewDecisionReason886 = "https://codesystem.x12.org/external/886";
    public const string X12ServiceType1365 = "https://codesystem.x12.org/005010/1365";
    public const string X12CertificationType1322 = "https://codesystem.x12.org/005010/1322";
    public const string X12RequestType1525 = "https://codesystem.x12.org/005010/1525";

    // Clinical / billing code systems
    public const string Cpt = "http://www.ama-assn.org/go/cpt";
    public const string Hcpcs = "https://bluebutton.cms.gov/resources/codesystem/hcpcs";
    public const string HcpcsOid = "urn:oid:2.16.840.1.113883.6.285";
    public const string Loinc = "http://loinc.org";
    public const string Icd10Cm = "http://hl7.org/fhir/sid/icd-10-cm";
    public const string PlaceOfService = "https://www.cms.gov/Medicare/Coding/place-of-service-codes/Place_of_Service_Code_Set";
}

public static class IdentifierSystems
{
    public const string Npi = "http://hl7.org/fhir/sid/us-npi";
}

/// <summary>OperationOutcome.issue.severity and issue.code values used by this API.</summary>
public static class IssueSeverity
{
    public const string Fatal = "fatal";
    public const string Error = "error";
    public const string Warning = "warning";
    public const string Information = "information";
}

public static class IssueType
{
    public const string Invalid = "invalid";
    public const string Required = "required";
    public const string Value = "value";
    public const string NotFound = "not-found";
    public const string NotSupported = "not-supported";
    public const string Duplicate = "duplicate";
    public const string BusinessRule = "business-rule";
    public const string Informational = "informational";
    public const string Exception = "exception";
}

/// <summary>SMART Backend Services scopes checked by the authorization policies.</summary>
public static class Scopes
{
    public const string ClaimCreate = "system/Claim.c";
    public const string ClaimWrite = "system/Claim.write";
    public const string QuestionnaireRead = "user/Questionnaire.r";
    public const string QuestionnaireReadSystem = "system/Questionnaire.read";
    public const string TaskRead = "system/Task.rs";
    public const string AttachmentSubmit = "system/DocumentReference.c";
}

public static class Policies
{
    public const string CrdHooks = "crd.hooks";
    public const string DtrRead = "dtr.read";
    public const string PasSubmit = "pas.submit";
    public const string CdexSubmit = "cdex.submit";
}
