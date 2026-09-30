using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Common;
using DaVinci.PriorAuth.Services.Crd;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Pas;

public interface IPasAdjudicator
{
    AdjudicationResult Adjudicate(PriorAuthRecord record, bool onlyPendedItems);
}

/// <summary>
/// Per-item decision for a PAS request. Decisions (X12 306):
///   C  cancelled item (certificationType 3 or infoCancelled)
///   A3 member not eligible / service not covered
///   CT code too generic - contact payer
///   NA no prior auth required (no rule, no-auth, outside PA place of service, gold card)
///   A4 pended - documentation (DTR QuestionnaireResponse) or attachments (CDex) missing,
///      or approval criteria not met -> clinical review by a UM nurse/physician
///   A2 approved with reduced quantity (requested > allowed)
///   A1 approved
/// A denial on clinical grounds is never automatic: it comes from a reviewer (UmReviewController).
/// </summary>
public class PasAdjudicator : IPasAdjudicator
{
    private readonly ICoverageRulesEngine _engine;
    private readonly PayerOptions _options;

    public PasAdjudicator(ICoverageRulesEngine engine, IOptions<PayerOptions> options)
    {
        _engine = engine; _options = options.Value;
    }

    public AdjudicationResult Adjudicate(PriorAuthRecord record, bool onlyPendedItems)
    {
        var bundle = record.RequestBundle;
        var claim = (Claim)bundle.Entry[0].Resource!;
        var resolver = new BundleResolver(bundle);
        var coverage = claim.Insurance.Where(i => i.Focal).Select(i => resolver.Resolve<Coverage>(i.Coverage)).FirstOrDefault();
        var providerNpi = record.ProviderNpi;

        var responses = CollectQuestionnaireResponses(bundle, record);
        var receivedLoinc = CollectAttachmentCodes(bundle, record);

        var result = new AdjudicationResult();
        foreach (var item in claim.Item)
        {
            var previous = record.Items.FirstOrDefault(i => i.Sequence == item.Sequence);
            if (onlyPendedItems && previous != null && previous.ReviewAction != X12ReviewActionCodes.Pended)
            {
                result.Items.Add(previous);   // keep final decisions
                continue;
            }

            var decision = DecideItem(item, claim, coverage, providerNpi, responses, receivedLoinc);
            if (decision.ReviewAction is X12ReviewActionCodes.CertifiedInTotal or X12ReviewActionCodes.CertifiedPartial)
            {
                decision.AuthNumber = record.PreAuthRef ??= NewAuthNumber();
            }
            result.Items.Add(decision);
            result.AttachmentsNeeded.AddRange(decision.MissingAttachments);
            result.QuestionnairesNeeded.AddRange(decision.MissingQuestionnaires.Select(q => (decision.Sequence, q)));
        }
        return result;
    }

    private PriorAuthItemRecord DecideItem(ClaimItem item, Claim claim, Coverage? coverage, string? providerNpi,
        List<QuestionnaireResponse> responses, HashSet<string> receivedLoinc)
    {
        var coding = item.ProductOrService?.Coding.FirstOrDefault();
        var d = new PriorAuthItemRecord
        {
            Sequence = item.Sequence,
            CodeSystem = coding?.System,
            ProductCode = coding?.Code ?? "",
            CategoryCode = item.Category?.Coding.FirstOrDefault()?.Code,
            RequestedQuantity = item.Quantity?.Value ?? 1
        };

        // Cancelled line (PAS Claim Update: certificationType 3 and/or modifierextension-infoCancelled)
        var certType = item.Extension.ConceptCode(PasExtensionUrls.CertificationType);
        if (certType == X12CertificationTypes.Cancel || item.ModifierExtension.IsTrue(PasExtensionUrls.InfoCancelled))
        {
            d.Cancelled = true;
            return Final(d, X12ReviewActionCodes.Cancelled, "Service line cancelled by the provider.");
        }

        // Eligibility
        if (coverage == null || coverage.Status != "active" || IsExpired(coverage, item))
            return Final(d, X12ReviewActionCodes.NotCertified, "Member not eligible for the requested date of service.");

        var rule = _engine.FindRule(coverage, item.ProductOrService?.Coding ?? new List<Coding>());
        d.RuleId = rule?.RuleId;

        if (rule?.Covered == CoveredCodes.NotCovered)
            return Final(d, X12ReviewActionCodes.NotCertified, rule.NotCoveredReason ?? "Service is not a covered benefit.");

        if (rule == null || rule.PaNeeded == PaNeededCodes.NoAuth)
            return Final(d, X12ReviewActionCodes.NoActionRequired, "No prior authorization is required for this service.");

        if (rule.RequiresMoreSpecificCode)
            return Final(d, X12ReviewActionCodes.ContactPayer, "Submit a specific procedure code instead of an unlisted code.");

        if (rule.PaRequiredPlacesOfService.Count > 0)
        {
            var pos = item.LocationCodeableConcept?.Coding.FirstOrDefault()?.Code;
            if (pos != null && !rule.PaRequiredPlacesOfService.Contains(pos))
                return Final(d, X12ReviewActionCodes.NoActionRequired, $"No prior authorization required at place of service {pos}.");
        }

        if (providerNpi != null && rule.GoldCardNpis.Contains(providerNpi))
            return Final(d, X12ReviewActionCodes.NoActionRequired, "Prior authorization waived: provider has gold-card status.");

        // ---- Documentation (DTR) and attachments (CDex)
        QuestionnaireResponse? qr = null;
        if (rule.DocPurpose.Contains(DocPurposeCodes.WithPa))
        {
            foreach (var url in rule.QuestionnaireUrls)
            {
                qr = responses.FirstOrDefault(r => QuestionnaireUrlOf(r) == url);
                if (qr == null) d.MissingQuestionnaires.Add(url);
            }
        }
        foreach (var loinc in rule.RequiredAttachmentLoinc.Where(l => !receivedLoinc.Contains(l)))
            d.MissingAttachments.Add(loinc);

        if (d.MissingQuestionnaires.Count > 0 || d.MissingAttachments.Count > 0)
        {
            d.ReviewAction = X12ReviewActionCodes.Pended;
            d.ReasonText = "Additional information requested: " +
                           string.Join(", ", d.MissingQuestionnaires.Concat(d.MissingAttachments.Select(l => $"LOINC {l}")));
            return d;
        }

        // ---- Clinical criteria
        var failed = rule.ApprovalCriteria.Where(c => !Meets(c, qr)).ToList();
        if (failed.Count > 0)
        {
            d.ReviewAction = X12ReviewActionCodes.Pended;
            d.ReasonText = "Pended for clinical review: " + string.Join("; ", failed.Select(f => f.Description));
            return d;
        }

        // ---- Approve (fully or partially)
        d.AuthorizedPeriod = AuthorizedPeriod(item, rule);
        if (rule.AllowedQuantity is int allowed && d.RequestedQuantity > allowed)
        {
            d.AuthorizedQuantity = allowed;
            d.ReviewAction = X12ReviewActionCodes.CertifiedPartial;
            d.ReasonText = $"Approved for {allowed} of {d.RequestedQuantity} units requested (plan limit).";
            return d;
        }

        d.AuthorizedQuantity = d.RequestedQuantity;
        d.ReviewAction = X12ReviewActionCodes.CertifiedInTotal;
        d.ReasonText = "Approved.";
        return d;
    }

    /// <summary>
    /// Canonical URL (without |version) of the Questionnaire a response answers. Adaptive DTR responses point to a
    /// contained Questionnaire ("#id"), so the URL is read from that contained copy.
    /// </summary>
    private static string? QuestionnaireUrlOf(QuestionnaireResponse response)
    {
        if (response.Questionnaire == null) return null;
        if (response.Questionnaire.StartsWith("#"))
        {
            var id = response.Questionnaire.Substring(1);
            var contained = response.Contained.OfType<Questionnaire>().FirstOrDefault(q => q.Id == id);
            return contained?.Url?.Split('|')[0];
        }
        return response.Questionnaire.Split('|')[0];
    }

        private static PriorAuthItemRecord Final(PriorAuthItemRecord d, string action, string reason)
    {
        d.ReviewAction = action;
        d.ReasonText = reason;
        return d;
    }

    private static bool Meets(ApprovalCriterion c, QuestionnaireResponse? qr)
    {
        var answer = qr?.FindItem(c.LinkId)?.Answer.FirstOrDefault();
        if (answer == null) return false;
        if (c.Operator == "true") return answer.ValueBoolean == true;

        var value = answer.ValueDecimal ?? answer.ValueInteger ?? answer.ValueQuantity?.Value;
        if (value == null || c.Number == null) return false;
        return c.Operator switch
        {
            ">=" => value >= c.Number,
            ">" => value > c.Number,
            "<=" => value <= c.Number,
            "<" => value < c.Number,
            "=" => value == c.Number,
            _ => false
        };
    }

    private Period AuthorizedPeriod(ClaimItem item, Models.Domain.CoverageRule rule)
    {
        if (item.ServicedPeriod?.Start != null) return item.ServicedPeriod;
        var start = FhirDate.TryParse(item.ServicedDate) ?? DateTimeOffset.UtcNow;
        var end = rule.AllowedPeriodMonths is int months ? start.AddMonths(months) : start.AddDays(_options.DefaultAuthValidityDays);
        return new Period { Start = FhirDate.Date(start), End = FhirDate.Date(end) };
    }

    private static bool IsExpired(Coverage coverage, ClaimItem item)
    {
        var end = FhirDate.TryParse(coverage.Period?.End);
        if (end == null) return false;
        var serviceDate = FhirDate.TryParse(item.ServicedDate ?? item.ServicedPeriod?.Start) ?? DateTimeOffset.UtcNow;
        return serviceDate > end.Value.AddDays(1);
    }

    private static string NewAuthNumber() =>
        $"PA{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(100000, 999999)}";

    /// <summary>QuestionnaireResponses sent in the PAS bundle or later through CDex (raw or wrapped in a DTR QR Bundle).</summary>
    private static List<QuestionnaireResponse> CollectQuestionnaireResponses(Bundle bundle, PriorAuthRecord record)
    {
        var list = bundle.ResourcesOf<QuestionnaireResponse>().ToList();
        foreach (var a in record.Attachments)
        {
            if (a.Content is QuestionnaireResponse qr) list.Add(qr);
            if (a.Content is Bundle b) list.AddRange(b.ResourcesOf<QuestionnaireResponse>());
        }
        return list.Where(q => q.Status is "completed" or "amended").ToList();
    }

    /// <summary>LOINC codes of documents received (DocumentReference.type in the bundle, or CDex attachment codes).</summary>
    private static HashSet<string> CollectAttachmentCodes(Bundle bundle, PriorAuthRecord record)
    {
        var codes = new HashSet<string>();
        foreach (var doc in bundle.ResourcesOf<DocumentReference>())
            foreach (var c in doc.Type?.Coding.Where(c => c.System == CodeSystems.Loinc) ?? Enumerable.Empty<Coding>())
                if (c.Code != null) codes.Add(c.Code);
        foreach (var a in record.Attachments)
        {
            if (a.LoincCode != null) codes.Add(a.LoincCode);
            if (a.Content is DocumentReference dr)
                foreach (var c in dr.Type?.Coding.Where(c => c.System == CodeSystems.Loinc) ?? Enumerable.Empty<Coding>())
                    if (c.Code != null) codes.Add(c.Code);
        }
        return codes;
    }
}
