using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Cdex;

public interface IAdditionalInfoRequestBuilder
{
    FhirTask BuildTask(PriorAuthRecord record, AdjudicationResult result);
    CommunicationRequest BuildCommunicationRequest(PriorAuthRecord record, AdjudicationResult result);
}

/// <summary>
/// When a PAS request is pended for missing information, the payer asks for it with:
///  - a PAS Task (profile-task, built on the CDex attachment-request pattern):
///      code  = attachment-request-code          (LOINC documents needed), or
///              attachment-request-questionnaire (a DTR questionnaire must be completed)
///      input = payer-url (where to $submit-attachment), attachments-needed / questionnaires-needed
///  - a PAS CommunicationRequest referenced from ClaimResponse.communicationRequest (X12 PWK/HI equivalent)
/// The provider answers with $submit-attachment using the Task's tracking id.
/// </summary>
public class AdditionalInfoRequestBuilder : IAdditionalInfoRequestBuilder
{
    private readonly PayerOptions _options;
    public AdditionalInfoRequestBuilder(IOptions<PayerOptions> options) { _options = options.Value; }

    public FhirTask BuildTask(PriorAuthRecord record, AdjudicationResult result)
    {
        var claim = (Claim)record.RequestBundle.Entry[0].Resource!;
        var needsQuestionnaire = result.QuestionnairesNeeded.Count > 0;

        var task = new FhirTask
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = TaskStatusCodes.Requested,
            Intent = "order",
            Code = new CodeableConcept(CodeSystems.PasTemp,
                needsQuestionnaire ? PasTempCodes.AttachmentRequestQuestionnaire : PasTempCodes.AttachmentRequestCode),
            For = new ResourceReference
            {
                Type = "Patient",
                Identifier = new Identifier(_options.MemberIdSystem, record.PatientMemberId),
                Reference = claim.Patient?.Reference
            },
            AuthoredOn = FhirDate.Now(),
            LastModified = FhirDate.Now(),
            Requester = ResourceReference.ByIdentifier(_options.PayerIdentifierSystem, _options.PayerIdentifierValue, "Organization"),
            Owner = record.ProviderNpi != null
                ? ResourceReference.ByIdentifier(IdentifierSystems.Npi, record.ProviderNpi)
                : claim.Provider,
            ReasonCode = new CodeableConcept(CodeSystems.PasTemp, PasTempCodes.PriorAuthorization, "Prior Authorization Information Request"),
            ReasonReference = new ResourceReference { Type = "Claim", Identifier = record.ClaimIdentifier },
            Restriction = new TaskRestriction
            {
                Period = new Period { End = FhirDate.Date(DateTimeOffset.UtcNow.AddDays(_options.AttachmentDueDays)) }
            }
        };
        task.WithProfile(PasProfiles.Task);

        // Payer tracking id - the provider echoes it as $submit-attachment TrackingId
        task.Identifier.Add(new Identifier(_options.TrackingIdSystem, record.TrackingId)
        {
            Type = new CodeableConcept(CodeSystems.CdexTemp, CdexTempCodes.TrackingId, "Tracking Id")
        });

        task.Input.Add(new TaskParameter
        {
            Type = new CodeableConcept(CodeSystems.PasTemp, PasTempCodes.PayerUrl),
            ValueUrl = $"{_options.BaseUrl}/fhir/{CdexOperations.SubmitAttachment}"
        });

        foreach (var item in result.Items)
        {
            foreach (var loinc in item.MissingAttachments)
            {
                var input = new TaskParameter
                {
                    Type = new CodeableConcept(CodeSystems.PasTemp, PasTempCodes.AttachmentsNeeded),
                    ValueCodeableConcept = new CodeableConcept(CodeSystems.Loinc, loinc)
                };
                input.Extension.Add(new Extension(PasExtensionUrls.PaLineNumber) { ValueInteger = item.Sequence });
                task.Input.Add(input);
            }

            foreach (var canonical in item.MissingQuestionnaires)
            {
                // PAS slice: questionnaires-needed (Identifier)
                var pasInput = new TaskParameter
                {
                    Type = new CodeableConcept(CodeSystems.PasTemp, PasTempCodes.QuestionnairesNeeded),
                    ValueIdentifier = new Identifier("urn:ietf:rfc:3986", canonical)
                };
                pasInput.Extension.Add(new Extension(PasExtensionUrls.PaLineNumber) { ValueInteger = item.Sequence });
                task.Input.Add(pasInput);

                // CDex/DTR input: questionnaire canonical the DTR app loads (sdc temp#questionnaire)
                var dtrInput = new TaskParameter
                {
                    Type = new CodeableConcept(CodeSystems.SdcTemp, "questionnaire"),
                    ValueCanonical = canonical
                };
                dtrInput.Extension.Add(new Extension(PasExtensionUrls.ServiceLineNumber) { ValuePositiveInt = item.Sequence });
                task.Input.Add(dtrInput);
            }
        }

        task.Input.Add(new TaskParameter
        {
            Type = new CodeableConcept(CodeSystems.CdexTemp, CdexTempCodes.PurposeOfUse),
            ValueCodeableConcept = new CodeableConcept(CodeSystems.ActReason, "COVAUTH", "coverage authorization")
        });
        return task;
    }

    public CommunicationRequest BuildCommunicationRequest(PriorAuthRecord record, AdjudicationResult result)
    {
        var claim = (Claim)record.RequestBundle.Entry[0].Resource!;
        var cr = new CommunicationRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = "active",
            Subject = claim.Patient,
            AuthoredOn = FhirDate.Now(),
            Requester = claim.Insurer,
            Recipient = { claim.Provider ?? new ResourceReference() }
        };
        cr.WithProfile(PasProfiles.CommunicationRequest);
        cr.Identifier.Add(new Identifier(_options.TrackingIdSystem, record.TrackingId));

        foreach (var item in result.Items)
        {
            foreach (var loinc in item.MissingAttachments)
            {
                var payload = new CommunicationRequestPayload { ContentString = $"Please send LOINC {loinc} for line {item.Sequence}." };
                payload.Extension.Add(new Extension(PasExtensionUrls.ServiceLineNumber) { ValuePositiveInt = item.Sequence });
                cr.Payload.Add(payload);
            }
            foreach (var q in item.MissingQuestionnaires)
            {
                var payload = new CommunicationRequestPayload { ContentString = $"Please complete questionnaire {q} (LOINC {LoincCodes.PayerQuestionnaire}) for line {item.Sequence}." };
                payload.Extension.Add(new Extension(PasExtensionUrls.ServiceLineNumber) { ValuePositiveInt = item.Sequence });
                cr.Payload.Add(payload);
            }
        }
        return cr;
    }
}
