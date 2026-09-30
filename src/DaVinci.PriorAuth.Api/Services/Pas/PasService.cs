using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Cdex;
using DaVinci.PriorAuth.Services.Common;
using DaVinci.PriorAuth.Services.Crd;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Pas;

/// <summary>HTTP status + FHIR body returned by the PAS operations.</summary>
public record FhirOperationResult(int StatusCode, Resource Body);

public interface IPasService
{
    Task<FhirOperationResult> SubmitAsync(Bundle request, CancellationToken ct);
    FhirOperationResult Inquire(Bundle request);
    Task<FhirOperationResult> RecordReviewerDecisionAsync(string recordId, int sequence, string action, string? reasonCode, string? reasonText, CancellationToken ct);
    Task ReadjudicateAsync(PriorAuthRecord record, CancellationToken ct);
}

public class PasService : IPasService
{
    private readonly IPriorAuthRepository _repo;
    private readonly IPasAdjudicator _adjudicator;
    private readonly IPasResponseBuilder _responses;
    private readonly IAdditionalInfoRequestBuilder _infoRequests;
    private readonly ISubscriptionService _subscriptions;
    private readonly IFhirResourceStore _store;
    private readonly EhrFhirClient _client;
    private readonly PayerOptions _options;
    private readonly ILogger<PasService> _log;

    public PasService(IPriorAuthRepository repo, IPasAdjudicator adjudicator, IPasResponseBuilder responses,
        IAdditionalInfoRequestBuilder infoRequests, ISubscriptionService subscriptions, IFhirResourceStore store,
        EhrFhirClient client, IOptions<PayerOptions> options, ILogger<PasService> log)
    {
        _repo = repo; _adjudicator = adjudicator; _responses = responses; _infoRequests = infoRequests;
        _subscriptions = subscriptions; _store = store; _client = client; _options = options.Value; _log = log;
    }

    // ================================================================== $submit

    public async Task<FhirOperationResult> SubmitAsync(Bundle request, CancellationToken ct)
    {
        // 1. Technical validation -> 400 + OperationOutcome
        var validation = PasRequestValidator.ValidateSubmit(request);
        if (validation.HasErrors) return new FhirOperationResult(400, validation);

        var claim = (Claim)request.Entry[0].Resource!;
        var resolver = new BundleResolver(request);

        // 2. Idempotency: same Bundle.identifier -> return the stored response, do not re-process
        var bundleKey = request.Identifier!.Key();
        var duplicate = _repo.FindByBundleIdentifier(bundleKey);
        if (duplicate?.LatestResponseBundle != null)
        {
            _log.LogInformation("Duplicate PAS submission {Bundle}; returning stored response", bundleKey);
            return new FhirOperationResult(200, duplicate.LatestResponseBundle);
        }

        // 3. Update / cancel of an earlier request (same Claim.identifier or Claim.related)
        var claimKey = claim.Identifier[0].Key();
        var relatedKey = claim.Related.Select(r => r.Claim?.Identifier?.Key() ?? r.Reference?.Key()).FirstOrDefault(k => k != null);
        var previous = _repo.FindActiveByClaimIdentifier(relatedKey ?? claimKey);

        var coverage = claim.Insurance.Where(i => i.Focal).Select(i => resolver.Resolve<Coverage>(i.Coverage)).First();
        var record = new PriorAuthRecord
        {
            BundleIdentifierKey = bundleKey,
            ClaimIdentifierKey = claimKey,
            ClaimIdentifier = claim.Identifier[0],
            PatientMemberId = MemberId.Of(coverage, _options) ?? "",
            ProviderNpi = ProviderNpi(claim, resolver),
            RequestorOrgIdentifierKey = RequestorOrgKey(claim, resolver),
            Priority = claim.Priority?.Coding.FirstOrDefault()?.Code ?? "normal",
            RequestBundle = request,
            TrackingId = previous?.TrackingId ?? ("TRK-" + Guid.NewGuid().ToString("N"))[..20],
            PreAuthRef = previous?.PreAuthRef,
            Version = (previous?.Version ?? 0) + 1
        };
        record.DueAt = record.SubmittedAt.AddHours(record.Priority == "stat" ? _options.ExpeditedDecisionHours : _options.StandardDecisionHours);
        if (previous != null)
        {
            record.Attachments.AddRange(previous.Attachments);   // documents already received still count
            previous.Status = PriorAuthStatus.Replaced;
            previous.ReplacedByRecordId = record.Id;
            _repo.Save(previous);
        }

        // 4. Adjudicate every line
        var result = _adjudicator.Adjudicate(record, onlyPendedItems: false);
        record.Items = result.Items;
        record.Status = StatusOf(record);

        // 5. Pended for missing information -> PAS Task (CDex) + CommunicationRequest
        CommunicationRequest? infoRequest = null;
        if (result.AttachmentsNeeded.Count > 0 || result.QuestionnairesNeeded.Count > 0)
        {
            infoRequest = _infoRequests.BuildCommunicationRequest(record, result);
            await CreateTaskAsync(record, result, ct);
        }

        // 6. Response bundle
        record.LatestResponseBundle = _responses.BuildResponseBundle(record, infoRequest);
        StoreClaimResponse(record);
        _repo.Save(record);

        _log.LogInformation("PAS {Claim} v{Version}: {Outcome}", claimKey, record.Version,
            string.Join(",", record.Items.Select(i => $"{i.Sequence}:{i.ReviewAction}")));
        return new FhirOperationResult(200, record.LatestResponseBundle);
    }

    // ================================================================== $inquire

    public FhirOperationResult Inquire(Bundle request)
    {
        var validation = PasRequestValidator.ValidateInquiry(request);
        if (validation.HasErrors) return new FhirOperationResult(400, validation);

        var claim = (Claim)request.Entry[0].Resource!;
        var resolver = new BundleResolver(request);
        var coverage = claim.Insurance.Select(i => resolver.Resolve<Coverage>(i.Coverage)).FirstOrDefault(c => c != null);
        var memberId = MemberId.Of(coverage, _options);

        // Query-by-example: Claim.identifier first, then member + service codes (+ provider)
        var matches = new List<PriorAuthRecord>();
        foreach (var id in claim.Identifier)
            if (_repo.FindActiveByClaimIdentifier(id.Key()) is { } r) matches.Add(r);

        if (matches.Count == 0 && memberId != null)
        {
            var codes = claim.Item.Select(i => i.ProductOrService?.Coding.FirstOrDefault()?.Code).Where(c => c != null).ToHashSet();
            var npi = ProviderNpi(claim, resolver);
            matches.AddRange(_repo.FindByMember(memberId).Where(r =>
                r.Status != PriorAuthStatus.Replaced &&
                (npi == null || r.ProviderNpi == npi) &&
                (codes.Count == 0 || r.Items.Any(i => codes.Contains(i.ProductCode)))));
        }

        var response = new Bundle { Id = Guid.NewGuid().ToString("N"), Type = "collection", Timestamp = FhirDate.Now(),
            Identifier = new Identifier($"{_options.BaseUrl}/inquiry-bundle-ids", Guid.NewGuid().ToString("N")) };
        response.WithProfile(PasProfiles.InquiryResponseBundle);

        foreach (var record in matches.Where(m => m.LatestResponseBundle != null))
        {
            var cr = record.LatestResponseBundle!.ResourcesOf<ClaimResponse>().First();
            response.Add(cr, $"urn:uuid:{cr.Id}");
        }
        if (response.Entry.Count == 0)
        {
            response.Add(OperationOutcome.Create(IssueSeverity.Information, IssueType.NotFound,
                "No prior authorization matched the inquiry."), $"urn:uuid:{Guid.NewGuid():N}");
        }
        else
        {
            // include the patient/insurer/requestor from the inquiry so references resolve
            foreach (var entry in request.Entry.Skip(1).Where(e => e.Resource is Patient or Organization or PractitionerRole))
                response.Entry.Add(entry);
        }
        return new FhirOperationResult(200, response);
    }

    // ================================================================== pended -> final

    /// <summary>A utilization-management reviewer decides a pended line (A1, A2, A3, A6, CT).</summary>
    public async Task<FhirOperationResult> RecordReviewerDecisionAsync(string recordId, int sequence, string action,
        string? reasonCode, string? reasonText, CancellationToken ct)
    {
        var record = _repo.Get(recordId);
        var item = record?.Items.FirstOrDefault(i => i.Sequence == sequence);
        if (record == null || item == null)
            return new FhirOperationResult(404, OperationOutcome.Create(IssueSeverity.Error, IssueType.NotFound, "Prior authorization or line not found."));
        if (item.ReviewAction != X12ReviewActionCodes.Pended)
            return new FhirOperationResult(409, OperationOutcome.Create(IssueSeverity.Error, IssueType.BusinessRule, "Line already has a final decision."));
        if (action is X12ReviewActionCodes.NotCertified && string.IsNullOrEmpty(reasonText) && string.IsNullOrEmpty(reasonCode))
            return new FhirOperationResult(400, OperationOutcome.Create(IssueSeverity.Error, IssueType.Required, "A denial requires a specific reason (CMS-0057-F)."));

        item.ReviewAction = action;
        item.ReasonCode = reasonCode;
        item.ReasonText = reasonText ?? X12ReviewActionCodes.Display(action);
        item.MissingAttachments.Clear();
        item.MissingQuestionnaires.Clear();
        if (action is X12ReviewActionCodes.CertifiedInTotal or X12ReviewActionCodes.CertifiedPartial or X12ReviewActionCodes.Modified)
        {
            item.AuthNumber = record.PreAuthRef ??= $"PA{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(100000, 999999)}";
            item.AuthorizedQuantity ??= item.RequestedQuantity;
            item.AuthorizedPeriod ??= new Period { Start = FhirDate.Today(), End = FhirDate.Date(DateTimeOffset.UtcNow.AddDays(_options.DefaultAuthValidityDays)) };
        }

        await FinishUpdateAsync(record, ct);
        return new FhirOperationResult(200, record.LatestResponseBundle!);
    }

    /// <summary>Called after CDex attachments arrive: re-run adjudication for pended lines only.</summary>
    public async Task ReadjudicateAsync(PriorAuthRecord record, CancellationToken ct)
    {
        var result = _adjudicator.Adjudicate(record, onlyPendedItems: true);
        record.Items = result.Items;

        foreach (var taskId in record.CdexTaskIds)
        {
            var task = _store.Get<FhirTask>(taskId);
            if (task == null || task.Status == TaskStatusCodes.Completed) continue;
            var stillMissing = result.AttachmentsNeeded.Count + result.QuestionnairesNeeded.Count;
            task.Status = stillMissing == 0 ? TaskStatusCodes.Completed : TaskStatusCodes.InProgress;
            task.LastModified = FhirDate.Now();
            _store.Save(task);
        }
        await FinishUpdateAsync(record, ct);
    }

    private async Task FinishUpdateAsync(PriorAuthRecord record, CancellationToken ct)
    {
        record.Status = StatusOf(record);
        record.LatestResponseBundle = _responses.BuildResponseBundle(record, null);
        StoreClaimResponse(record);
        _repo.Save(record);
        await _subscriptions.NotifyAsync(record, ct);   // tell the provider the result changed
    }

    // ================================================================== helpers

    private async Task CreateTaskAsync(PriorAuthRecord record, AdjudicationResult result, CancellationToken ct)
    {
        var task = _infoRequests.BuildTask(record, result);
        _store.Save(task);
        record.CdexTaskIds.Add(task.Id!);
        if (!string.IsNullOrEmpty(_options.ProviderTaskEndpoint))
            await _client.PostAsync($"{_options.ProviderTaskEndpoint.TrimEnd('/')}/Task", task, ct);
    }

    /// <summary>Keep the latest ClaimResponse readable at GET /fhir/ClaimResponse/{id} (full-resource notifications point here).</summary>
    private void StoreClaimResponse(PriorAuthRecord record)
    {
        var cr = record.LatestResponseBundle?.ResourcesOf<ClaimResponse>().FirstOrDefault();
        if (cr != null) _store.Save(cr);
    }

    private static PriorAuthStatus StatusOf(PriorAuthRecord record) =>
        record.Items.All(i => i.Cancelled) ? PriorAuthStatus.Cancelled
        : record.Items.Any(i => i.ReviewAction == X12ReviewActionCodes.Pended) ? PriorAuthStatus.Pended
        : PriorAuthStatus.Complete;

    private static string? ProviderNpi(Claim claim, BundleResolver resolver)
    {
        if (claim.Provider?.Identifier?.System == IdentifierSystems.Npi) return claim.Provider.Identifier.Value;
        var org = resolver.Resolve<Organization>(claim.Provider);
        if (org != null) return org.Identifier.FirstOrDefault(i => i.System == IdentifierSystems.Npi)?.Value;
        var role = resolver.Resolve<PractitionerRole>(claim.Provider);
        var practitioner = resolver.Resolve<Practitioner>(role?.Practitioner);
        return practitioner?.Identifier.FirstOrDefault(i => i.System == IdentifierSystems.Npi)?.Value
               ?? role?.Identifier.FirstOrDefault(i => i.System == IdentifierSystems.Npi)?.Value;
    }

    /// <summary>Requesting organization identifier (used to match PAS subscriptions: orgIdentifier).</summary>
    private static string? RequestorOrgKey(Claim claim, BundleResolver resolver)
    {
        var org = resolver.Resolve<Organization>(claim.Provider)
                  ?? resolver.Resolve<Organization>(resolver.Resolve<PractitionerRole>(claim.Provider)?.Organization);
        return org?.Identifier.FirstOrDefault()?.Key() ?? claim.Provider?.Identifier?.Key();
    }
}
