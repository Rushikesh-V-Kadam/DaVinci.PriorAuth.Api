using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Pas;

public interface IPasResponseBuilder
{
    Bundle BuildResponseBundle(PriorAuthRecord record, CommunicationRequest? infoRequest, bool inquiry = false);
    ClaimResponse BuildClaimResponse(PriorAuthRecord record, CommunicationRequest? infoRequest);
}

/// <summary>Builds the PAS Response Bundle: ClaimResponse first, then the resources it references.</summary>
public class PasResponseBuilder : IPasResponseBuilder
{
    private readonly PayerOptions _options;
    public PasResponseBuilder(IOptions<PayerOptions> options) { _options = options.Value; }

    public Bundle BuildResponseBundle(PriorAuthRecord record, CommunicationRequest? infoRequest, bool inquiry = false)
    {
        var request = record.RequestBundle;
        var claim = (Claim)request.Entry[0].Resource!;

        var bundle = new Bundle
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "collection",
            Timestamp = FhirDate.Now(),
            Identifier = new Identifier($"{_options.BaseUrl}/response-bundle-ids", Guid.NewGuid().ToString("N"))
        };
        bundle.WithProfile(inquiry ? PasProfiles.InquiryResponseBundle : PasProfiles.ResponseBundle);

        var claimResponse = BuildClaimResponse(record, infoRequest);
        bundle.Add(claimResponse, $"urn:uuid:{claimResponse.Id}");

        // Referenced resources, copied from the request so references resolve inside the response bundle
        void AddFromRequest(ResourceReference? reference)
        {
            if (reference?.Reference == null) return;
            var entry = request.Entry.FirstOrDefault(e => e.FullUrl == reference.Reference ||
                (e.Resource != null && reference.Reference.EndsWith($"{e.Resource.ResourceType}/{e.Resource.Id}", StringComparison.Ordinal)));
            if (entry?.Resource != null && bundle.Entry.All(e => e.Resource != entry.Resource))
                bundle.Add(entry.Resource, entry.FullUrl);
        }
        AddFromRequest(claim.Patient);
        AddFromRequest(claim.Insurer);
        AddFromRequest(claim.Provider);

        if (infoRequest != null) bundle.Add(infoRequest, $"urn:uuid:{infoRequest.Id}");
        return bundle;
    }

    public ClaimResponse BuildClaimResponse(PriorAuthRecord record, CommunicationRequest? infoRequest)
    {
        var claim = (Claim)record.RequestBundle.Entry[0].Resource!;
        var claimFullUrl = record.RequestBundle.Entry[0].FullUrl;

        var cr = new ClaimResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = "active",
            Type = claim.Type,
            Use = "preauthorization",
            Patient = claim.Patient,
            Created = FhirDate.Now(),
            Insurer = claim.Insurer,
            Requestor = claim.Provider,
            Request = claimFullUrl != null
                ? new ResourceReference(claimFullUrl)
                : new ResourceReference { Identifier = claim.Identifier.FirstOrDefault() },
            Outcome = record.Items.Any(i => i.ReviewAction == X12ReviewActionCodes.Pended) ? ClaimOutcomes.Queued : ClaimOutcomes.Complete,
            Disposition = Disposition(record),
            PreAuthRef = record.Items.Any(i => i.AuthNumber != null) ? record.PreAuthRef : null
        };
        cr.WithProfile(PasProfiles.ClaimResponse);
        cr.Identifier.Add(new Identifier(_options.AuthNumberSystem, record.Id));

        var approved = record.Items.Where(i => i.AuthorizedPeriod != null).ToList();
        if (approved.Count > 0)
        {
            cr.PreAuthPeriod = new Period
            {
                Start = approved.Min(i => i.AuthorizedPeriod!.Start),
                End = approved.Max(i => i.AuthorizedPeriod!.End)
            };
        }

        var claimItems = claim.Item.ToDictionary(i => i.Sequence);
        foreach (var item in record.Items)
        {
            var responseItem = new ClaimResponseItem { ItemSequence = item.Sequence };

            // Echo the provider's item trace number (2000F TRN) if one was sent
            if (claimItems.TryGetValue(item.Sequence, out var claimItem))
                foreach (var trace in claimItem.Extension.GetAllByUrl(PasExtensionUrls.ItemTraceNumber))
                    responseItem.Extension.Add(trace);

            if (item.AuthorizedPeriod != null)
            {
                responseItem.Extension.Add(Extension.Date(PasExtensionUrls.ItemPreAuthIssueDate, FhirDate.Today()));
                responseItem.Extension.Add(new Extension(PasExtensionUrls.ItemPreAuthPeriod) { ValuePeriod = item.AuthorizedPeriod });
            }

            if (item.ReviewAction == X12ReviewActionCodes.CertifiedPartial && item.AuthorizedQuantity != null)
            {
                responseItem.Extension.Add(Extension.Complex(PasExtensionUrls.ItemAuthorizedDetail,
                    new Extension("quantity") { ValueQuantity = new Quantity { Value = item.AuthorizedQuantity } }));
            }

            // PAS requires at least one adjudication with category "submitted".
            // PAS 2.0.1: the line decision (reviewAction) is an extension of that adjudication, not of the item.
            var adjudication = new ClaimResponseAdjudication
            {
                Category = new CodeableConcept(CodeSystems.Adjudication, "submitted"),
                Value = item.RequestedQuantity
            };
            adjudication.Extension.Add(ReviewAction(item));
            responseItem.Adjudication.Add(adjudication);
            cr.Item.Add(responseItem);
        }

        if (infoRequest != null) cr.CommunicationRequest.Add(new ResourceReference($"urn:uuid:{infoRequest.Id}"));

        var noteNumber = 1;
        foreach (var item in record.Items.Where(i => i.ReasonText != null))
        {
            cr.ProcessNote.Add(new ClaimResponseProcessNote { Number = noteNumber, Type = "display", Text = $"Line {item.Sequence}: {item.ReasonText}" });
            cr.Item.First(i => i.ItemSequence == item.Sequence).NoteNumber.Add(noteNumber++);
        }
        return cr;
    }

    /// <summary>extension-reviewAction: code (X12 306) + number (auth/review number) + reasonCode (X12 886).</summary>
    private static Extension ReviewAction(PriorAuthItemRecord item)
    {
        var ext = new Extension(PasExtensionUrls.ReviewAction);
        ext.Extension_.Add(Extension.Concept(PasExtensionUrls.ReviewActionCode,
            new CodeableConcept(CodeSystems.X12ReviewAction306, item.ReviewAction, X12ReviewActionCodes.Display(item.ReviewAction))));
        if (item.AuthNumber != null)
            ext.Extension_.Add(Extension.String(ReviewActionParts.Number, item.AuthNumber));
        if (item.ReasonCode != null)
            ext.Extension_.Add(Extension.Concept(ReviewActionParts.ReasonCode,
                new CodeableConcept(CodeSystems.X12ReviewDecisionReason886, item.ReasonCode, text: item.ReasonText)));
        return ext;
    }

    private static string Disposition(PriorAuthRecord record)
    {
        if (record.Status == PriorAuthStatus.Cancelled) return "Prior authorization request cancelled.";
        var counts = record.Items.GroupBy(i => i.ReviewAction)
            .Select(g => $"{g.Count()} {X12ReviewActionCodes.Display(g.Key).ToLowerInvariant()}");
        return string.Join(", ", counts);
    }
}
