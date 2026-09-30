using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Crd;

public interface ICoverageRulesEngine
{
    CoverageDecision Evaluate(OrderContext order, Coverage coverage);
    CoverageRule? FindRule(Coverage? coverage, IEnumerable<Coding> codes);
}

/// <summary>
/// Decides covered / pa-needed / doc-needed for one order. The order of the checks matters and
/// follows the scenario table in the guide:
///   1 no matching rule        -> covered, no-auth (plan default)
///   2 not covered             -> not-covered
///   3 code too generic        -> conditional + detail-code
///   4 PA depends on location  -> conditional + info-needed=location (or resolved by POS)
///   5 existing approval       -> satisfied + satisfied-pa-id
///   6 gold-card provider      -> no-auth + reason gold-card
///   7 rule PA / documentation -> auth-needed | performpa | no-auth, doc-needed, questionnaires
/// </summary>
public class CoverageRulesEngine : ICoverageRulesEngine
{
    private readonly ICoverageRuleRepository _rules;
    private readonly IPriorAuthRepository _priorAuths;
    private readonly PayerOptions _options;

    public CoverageRulesEngine(ICoverageRuleRepository rules, IPriorAuthRepository priorAuths, IOptions<PayerOptions> options)
    {
        _rules = rules; _priorAuths = priorAuths; _options = options.Value;
    }

    public CoverageRule? FindRule(Coverage? coverage, IEnumerable<Coding> codes) =>
        _rules.Find(coverage?.PlanId(), codes);

    public CoverageDecision Evaluate(OrderContext order, Coverage coverage)
    {
        var decision = new CoverageDecision();
        var rule = FindRule(coverage, order.Codes);
        var billing = rule != null
            ? order.Codes.FirstOrDefault(c => c.Code == rule.Code)
            : order.Codes.FirstOrDefault();
        if (billing != null) decision.BillingCodes.Add(billing);

        // 1. No policy row: plan default is covered, no PA, no documentation
        if (rule == null)
        {
            decision.Covered = CoveredCodes.Covered;
            decision.PaNeeded = PaNeededCodes.NoAuth;
            decision.Summary = "Covered. No prior authorization or additional documentation required.";
            return decision;
        }

        decision.RuleId = rule.RuleId;
        decision.PolicyUrl = rule.PolicyUrl;

        // 2. Not covered
        if (rule.Covered == CoveredCodes.NotCovered)
        {
            decision.Covered = CoveredCodes.NotCovered;
            if (rule.NotCoveredReason != null) decision.Reasons.Add(CodeableConcept.FromText(rule.NotCoveredReason));
            decision.Summary = $"{rule.Description} is not covered by this plan.";
            return decision;
        }

        // 3. Code too generic to decide
        if (rule.RequiresMoreSpecificCode)
        {
            decision.Covered = CoveredCodes.Conditional;
            decision.PaNeeded = PaNeededCodes.Conditional;
            decision.Details.Add(new CoverageDetail { Category = CoverageDetailCodes.DetailCode, ValueBoolean = true });
            decision.Summary = $"{rule.Code} is too general to determine coverage. Order a more specific code.";
            return decision;
        }

        decision.Covered = rule.Covered;

        // 4. PA depends on place of service
        var paNeeded = rule.PaNeeded;
        if (rule.PaRequiredPlacesOfService.Count > 0)
        {
            if (order.PlaceOfServiceCode == null)
            {
                decision.PaNeeded = PaNeededCodes.Conditional;
                decision.InfoNeeded.Add(InfoNeededCodes.Location);
                decision.Summary = $"Prior authorization for {rule.Description} depends on where it is performed. Add the location to the order.";
                return decision;
            }
            if (!rule.PaRequiredPlacesOfService.Contains(order.PlaceOfServiceCode))
            {
                decision.PaNeeded = PaNeededCodes.NoAuth;
                decision.Summary = $"{rule.Description} at place of service {order.PlaceOfServiceCode} does not need prior authorization.";
                return decision;
            }
        }

        // 5. Existing approved authorization covers this service
        if (paNeeded is PaNeededCodes.AuthNeeded or PaNeededCodes.PerformPa)
        {
            var existing = FindActiveApproval(coverage, rule.Code);
            if (existing != null)
            {
                decision.PaNeeded = PaNeededCodes.Satisfied;
                decision.SatisfiedPaId = existing;
                decision.Summary = $"Prior authorization {existing} already approved for {rule.Description}.";
                return decision;
            }
        }

        // 6. Gold card: PA waived for this ordering practitioner
        if (paNeeded == PaNeededCodes.AuthNeeded && order.RequesterNpi != null && rule.GoldCardNpis.Contains(order.RequesterNpi))
        {
            decision.PaNeeded = PaNeededCodes.NoAuth;
            decision.Reasons.Add(new CodeableConcept(CodeSystems.CrdTemp, CrdReasonCodes.GoldCard, "Gold card"));
            decision.Summary = $"Prior authorization waived: ordering provider has gold-card status for {rule.Description}.";
            return decision;
        }

        // 7. Rule-driven PA + documentation
        decision.PaNeeded = paNeeded;
        if (rule.DocNeeded != null)
        {
            decision.DocNeeded = rule.DocNeeded;
            decision.DocPurpose.AddRange(rule.DocPurpose);
            decision.QuestionnaireUrls.AddRange(rule.QuestionnaireUrls);
        }
        if (rule.AllowedQuantity is int qty)
            decision.Details.Add(new CoverageDetail
            {
                Category = CoverageDetailCodes.AllowedQuantity,
                ValueQuantity = new Quantity { Value = qty }
            });

        decision.Summary = (decision.PaNeeded, decision.DocNeeded) switch
        {
            (PaNeededCodes.AuthNeeded, not null) => $"Prior authorization required for {rule.Description}. Complete the documentation (DTR), then submit the request (PAS).",
            (PaNeededCodes.AuthNeeded, null) => $"Prior authorization required for {rule.Description}. Submit the request (PAS).",
            (PaNeededCodes.PerformPa, _) => $"Prior authorization required for {rule.Description}; it must be requested by the performing provider.",
            (_, not null) => $"No prior authorization needed for {rule.Description}, but documentation is required ({string.Join(", ", decision.DocPurpose)}).",
            _ => $"{rule.Description} is covered. No prior authorization required."
        };
        return decision;
    }

    /// <summary>Returns the preAuthRef of an approved, unexpired authorization for this member + code.</summary>
    private string? FindActiveApproval(Coverage coverage, string code)
    {
        var memberId = MemberId.Of(coverage, _options);
        if (memberId == null) return null;
        var today = DateTimeOffset.UtcNow;

        foreach (var record in _priorAuths.FindByMember(memberId).Where(r => r.Status == PriorAuthStatus.Complete))
        {
            var item = record.Items.FirstOrDefault(i =>
                i.ProductCode == code && !i.Cancelled &&
                i.ReviewAction is X12ReviewActionCodes.CertifiedInTotal or X12ReviewActionCodes.CertifiedPartial or X12ReviewActionCodes.Modified &&
                (FhirDate.TryParse(i.AuthorizedPeriod?.End) is not { } end || end >= today));
            if (item != null) return item.AuthNumber ?? record.PreAuthRef;
        }
        return null;
    }
}

public static class MemberId
{
    /// <summary>Member id from Coverage.subscriberId, else Coverage.identifier with the member-id system.</summary>
    public static string? Of(Coverage? coverage, PayerOptions options) =>
        coverage == null ? null
        : coverage.SubscriberId
          ?? coverage.Identifier.FirstOrDefault(i => i.System == options.MemberIdSystem)?.Value
          ?? coverage.Identifier.FirstOrDefault()?.Value;
}
