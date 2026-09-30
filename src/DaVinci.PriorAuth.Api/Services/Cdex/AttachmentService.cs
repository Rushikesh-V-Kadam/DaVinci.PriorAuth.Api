using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Pas;

namespace DaVinci.PriorAuth.Services.Cdex;

public interface IAttachmentService
{
    Task<FhirOperationResult> SubmitAsync(Parameters input, CancellationToken ct);
}

/// <summary>
/// CDex $submit-attachment (POST [base]/$submit-attachment).
///  - Solicited: TrackingId = the tracking id from the payer's Task (identifier slice tracking-id)
///  - Unsolicited: TrackingId = the provider's Claim.identifier value sent in the PAS request
/// AttachTo = preauthorization links the documents to a PAS request and re-runs adjudication
/// (when Final is true, the default). AttachTo = claim is accepted and stored for claims processing.
/// </summary>
public class AttachmentService : IAttachmentService
{
    private readonly IPriorAuthRepository _repo;
    private readonly IPasService _pas;
    private readonly IFhirResourceStore _store;
    private readonly ILogger<AttachmentService> _log;

    public AttachmentService(IPriorAuthRepository repo, IPasService pas, IFhirResourceStore store, ILogger<AttachmentService> log)
    {
        _repo = repo; _pas = pas; _store = store; _log = log;
    }

    public async Task<FhirOperationResult> SubmitAsync(Parameters input, CancellationToken ct)
    {
        // ---- validate parameters (CDex 2.0.0 OperationDefinition submit-attachment)
        var oo = new OperationOutcome();
        var trackingId = input.GetSingle(CdexParams.TrackingId)?.ValueIdentifier;
        var attachTo = input.GetSingle(CdexParams.AttachTo)?.ValueCode;
        var memberId = input.GetSingle(CdexParams.MemberId)?.ValueIdentifier;
        var providerId = input.GetSingle(CdexParams.ProviderId)?.ValueIdentifier;
        var organizationId = input.GetSingle(CdexParams.OrganizationId)?.ValueIdentifier;
        var isFinal = input.GetSingle(CdexParams.Final)?.ValueBoolean ?? true;
        var attachments = input.Get(CdexParams.Attachment).ToList();

        if (string.IsNullOrEmpty(trackingId?.Value)) oo.Add(IssueSeverity.Error, IssueType.Required, "TrackingId is required.", "Parameters.parameter[TrackingId]");
        if (attachTo is not (CdexAttachTo.Claim or CdexAttachTo.PreAuthorization))
            oo.Add(IssueSeverity.Error, IssueType.Value, "AttachTo SHALL be 'claim' or 'preauthorization'.", "Parameters.parameter[AttachTo]");
        if (string.IsNullOrEmpty(memberId?.Value)) oo.Add(IssueSeverity.Error, IssueType.Required, "MemberId is required.", "Parameters.parameter[MemberId]");
        if (providerId == null && organizationId == null)
            oo.Add(IssueSeverity.Error, IssueType.Required, "ProviderId or OrganizationId is required.", "Parameters.parameter[ProviderId]");
        if (attachments.Count == 0) oo.Add(IssueSeverity.Error, IssueType.Required, "At least one Attachment is required.", "Parameters.parameter[Attachment]");
        for (var i = 0; i < attachments.Count; i++)
            if (attachments[i].Part.FirstOrDefault(p => p.Name == CdexParams.Content)?.Resource == null)
                oo.Add(IssueSeverity.Error, IssueType.Required, "Attachment.Content (a FHIR resource) is required.", $"Parameters.parameter[Attachment][{i}]");
        if (oo.HasErrors) return new FhirOperationResult(400, oo);

        var received = attachments.Select(ToReceived).ToList();

        // ---- claim attachments: store only (claims processing is outside this API)
        if (attachTo == CdexAttachTo.Claim)
        {
            foreach (var a in received.Where(a => a.Content != null)) _store.Save(a.Content!);
            return new FhirOperationResult(200, OperationOutcome.Create(IssueSeverity.Information, IssueType.Informational,
                $"{received.Count} claim attachment(s) received for tracking id {trackingId!.Value}."));
        }

        // ---- prior authorization attachments
        var record = _repo.FindByTrackingId(trackingId!.Value!)
                     ?? _repo.FindActiveByClaimIdentifier(trackingId.Key())
                     ?? _repo.FindActiveByClaimIdentifier($"{trackingId.System}|{trackingId.Value}");
        if (record == null)
            return new FhirOperationResult(404, OperationOutcome.Create(IssueSeverity.Error, IssueType.NotFound,
                $"No prior authorization found for tracking id {trackingId.Value}."));

        if (!string.Equals(record.PatientMemberId, memberId!.Value, StringComparison.Ordinal))
            return new FhirOperationResult(422, OperationOutcome.Create(IssueSeverity.Error, IssueType.BusinessRule,
                "MemberId does not match the member on the prior authorization."));

        record.Attachments.AddRange(received);
        _repo.Save(record);
        _log.LogInformation("CDex: {Count} attachment(s) for {Tracking} (final={Final})", received.Count, record.TrackingId, isFinal);

        if (isFinal && record.Status == PriorAuthStatus.Pended)
            await _pas.ReadjudicateAsync(record, ct);

        return new FhirOperationResult(200, OperationOutcome.Create(IssueSeverity.Information, IssueType.Informational,
            $"{received.Count} attachment(s) received for prior authorization {record.ClaimIdentifier.Value}. " +
            $"Status: {record.Status}."));
    }

    private static ReceivedAttachment ToReceived(ParametersParameter attachment)
    {
        var content = attachment.Part.First(p => p.Name == CdexParams.Content).Resource!;
        var code = attachment.Part.FirstOrDefault(p => p.Name == CdexParams.Code)?.ValueCodeableConcept?
            .Coding.FirstOrDefault(c => c.System == CodeSystems.Loinc)?.Code;
        if (code == null && content is DocumentReference dr)
            code = dr.Type?.Coding.FirstOrDefault(c => c.System == CodeSystems.Loinc)?.Code;

        return new ReceivedAttachment
        {
            LoincCode = code,
            LineItems = attachment.Part.Where(p => p.Name == CdexParams.LineItem)
                .Select(p => int.TryParse(p.ValueString, out var n) ? n : 0).Where(n => n > 0).ToList(),
            ContentResourceType = content.ResourceType,
            Content = content
        };
    }
}
