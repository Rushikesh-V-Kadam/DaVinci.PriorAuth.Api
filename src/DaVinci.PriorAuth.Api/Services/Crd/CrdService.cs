using System.Text.Json;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.CdsHooks;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Crd;

public interface ICrdService
{
    CdsDiscoveryResponse Discovery();
    Task<CdsHookResponse> HandleHookAsync(string hook, CdsHookRequest request, CancellationToken ct);
}

public class CrdService : ICrdService
{
    private readonly OrderExtractor _extractor;
    private readonly ICoverageRulesEngine _engine;
    private readonly ICoverageAssertionRepository _assertions;
    private readonly PayerOptions _options;
    private readonly ILogger<CrdService> _log;

    public CrdService(OrderExtractor extractor, ICoverageRulesEngine engine, ICoverageAssertionRepository assertions,
        IOptions<PayerOptions> options, ILogger<CrdService> log)
    {
        _extractor = extractor; _engine = engine; _assertions = assertions; _options = options.Value; _log = log;
    }

    // ------------------------------------------------------------------ discovery

    public CdsDiscoveryResponse Discovery()
    {
        var response = new CdsDiscoveryResponse();
        foreach (var hook in CrdHooks.All)
        {
            response.Services.Add(new CdsServiceDefinition
            {
                Hook = hook,
                Id = CrdHooks.ServiceId(hook),
                Title = $"{_options.PayerName} Coverage Requirements Discovery ({hook})",
                Description = "Returns coverage, prior authorization and documentation requirements (Da Vinci CRD 2.0.1).",
                Prefetch = PrefetchFor(hook),
                Extension = new Dictionary<string, object>
                {
                    [CrdExtensionUrls.ConfigurationOptions] = new List<CrdConfigurationOption>
                    {
                        new() { Code = CrdCardTypes.CoverageInfo, Type = "boolean", Name = "Coverage Information",
                                Description = "Return coverage, prior authorization and documentation requirements.", Default = true },
                        new() { Code = CrdCardTypes.Claim, Type = "boolean", Name = "Claim information",
                                Description = "Return information about documentation needed with the claim.", Default = true },
                        new() { Code = CrdCardTypes.MaxCards, Type = "integer", Name = "Maximum cards",
                                Description = "Maximum number of cards returned for one hook call.", Default = 10 }
                    }
                }
            });
        }
        return response;
    }

    private static Dictionary<string, string> PrefetchFor(string hook)
    {
        var p = new Dictionary<string, string>
        {
            [CrdPrefetchKeys.Coverage] = "Coverage?patient={{context.patientId}}&status=active",
            [CrdPrefetchKeys.Patient] = "Patient/{{context.patientId}}"
        };
        switch (hook)
        {
            case CrdHooks.OrderSign:
            case CrdHooks.OrderSelect:
                p[CrdPrefetchKeys.ServiceRequest] = "ServiceRequest?_id={{context.draftOrders.ServiceRequest.id}}&_include=ServiceRequest:patient&_include=ServiceRequest:performer&_include=ServiceRequest:requester";
                p[CrdPrefetchKeys.DeviceRequest] = "DeviceRequest?_id={{context.draftOrders.DeviceRequest.id}}&_include=DeviceRequest:patient&_include=DeviceRequest:performer&_include=DeviceRequest:requester&_include=DeviceRequest:device";
                p[CrdPrefetchKeys.MedicationRequest] = "MedicationRequest?_id={{context.draftOrders.MedicationRequest.id}}&_include=MedicationRequest:patient&_include=MedicationRequest:intended-dispenser&_include=MedicationRequest:requester:PractitionerRole&_include=MedicationRequest:medication";
                break;
            case CrdHooks.OrderDispatch:
                p[CrdPrefetchKeys.Order] = "{{context.order}}";
                break;
            case CrdHooks.AppointmentBook:
                p[CrdPrefetchKeys.Appointment] = "Appointment?_id={{context.appointments.Appointment.id}}&_include=Appointment:patient&_include=Appointment:practitioner:PractitionerRole";
                break;
            case CrdHooks.EncounterStart:
            case CrdHooks.EncounterDischarge:
                p[CrdPrefetchKeys.Encounter] = "Encounter?_id={{context.encounterId}}&_include=Encounter:patient&_include=Encounter:service-provider&_include=Encounter:practitioner&_include=Encounter:location";
                break;
        }
        return p;
    }

    // ------------------------------------------------------------------ hook handling

    public async Task<CdsHookResponse> HandleHookAsync(string hook, CdsHookRequest request, CancellationToken ct)
    {
        var response = new CdsHookResponse();
        var config = ReadConfiguration(request);

        var coverage = await _extractor.GetCoverageAsync(request, _options.PayerIdentifierValue, ct);
        if (coverage == null)
        {
            // Without Coverage we cannot answer; tell the user instead of failing the hook.
            response.Cards.Add(Card(CdsIndicators.Warning,
                "No active coverage found for this payer",
                "Coverage requirements could not be determined because no active Coverage was provided. " +
                "Update the patient's insurance information and try again.", CrdCardTypes.Insurance));
            return response;
        }

        var orders = await _extractor.GetOrdersAsync(request, ct);
        var coverageRef = new ResourceReference($"Coverage/{coverage.Id}");
        var memberId = MemberId.Of(coverage, _options) ?? "";
        var systemActions = new List<CdsAction>();

        foreach (var order in orders)
        {
            var decision = _engine.Evaluate(order, coverage);

            _assertions.Save(new CoverageAssertion
            {
                CoverageAssertionId = decision.CoverageAssertionId,
                PatientMemberId = memberId,
                OrderReference = order.Reference,
                BillingCode = decision.BillingCodes.FirstOrDefault()?.Code,
                Decision = decision
            });

            _log.LogInformation("CRD {Hook} {Order}: covered={Covered} pa={Pa} doc={Doc}",
                hook, order.Reference, decision.Covered, decision.PaNeeded, decision.DocNeeded);

            // Coverage Information -> systemAction "update" on the order (mandatory for primary hooks)
            if (config.CoverageInfo)
            {
                var extension = CoverageInformationBuilder.Build(decision, coverageRef);
                systemActions.Add(new CdsAction
                {
                    Type = CdsActionTypes.Update,
                    Description = $"Add coverage information to {order.Reference}",
                    Resource = CoverageInformationBuilder.ApplyTo(order.Raw, extension)
                });
            }

            response.Cards.AddRange(CardsFor(decision, order, coverage));
        }

        if (systemActions.Count > 0) response.SystemActions = systemActions;
        if (response.Cards.Count > config.MaxCards) response.Cards = response.Cards.Take(config.MaxCards).ToList();
        return response;
    }

    private IEnumerable<CdsCard> CardsFor(CoverageDecision d, OrderContext order, Coverage coverage)
    {
        var what = d.BillingCodes.FirstOrDefault() is { } c ? $"{c.Display ?? c.Code}" : order.Reference;

        if (d.Covered == CoveredCodes.NotCovered)
        {
            yield return Card(CdsIndicators.Warning, Truncate($"Not covered: {what}"),
                d.Reasons.FirstOrDefault()?.Text ?? d.Summary, CrdCardTypes.CoverageInfo, d.PolicyUrl);
            yield break;
        }

        if (d.Covered == CoveredCodes.Conditional || d.PaNeeded == PaNeededCodes.Conditional)
        {
            var needed = d.InfoNeeded.Count > 0 ? $" Information needed: {string.Join(", ", d.InfoNeeded)}." : "";
            yield return Card(CdsIndicators.Info, Truncate($"More information needed for {what}"), d.Summary + needed, CrdCardTypes.CoverageInfo);
            yield break;
        }

        switch (d.PaNeeded)
        {
            case PaNeededCodes.AuthNeeded:
                yield return Card(CdsIndicators.Info, Truncate($"Prior authorization required: {what}"), d.Summary, CrdCardTypes.CoverageInfo, d.PolicyUrl);
                break;
            case PaNeededCodes.PerformPa:
                yield return Card(CdsIndicators.Info, Truncate($"Performing provider must request prior auth: {what}"), d.Summary, CrdCardTypes.CoverageInfo);
                break;
            case PaNeededCodes.Satisfied:
                yield return Card(CdsIndicators.Info, Truncate($"Prior authorization on file: {d.SatisfiedPaId}"), d.Summary, CrdCardTypes.CoverageInfo);
                break;
            case PaNeededCodes.NoAuth when d.Reasons.Any(r => r.HasCode(CodeSystems.CrdTemp, CrdReasonCodes.GoldCard)):
                yield return Card(CdsIndicators.Info, Truncate($"Prior authorization waived (gold card): {what}"), d.Summary, CrdCardTypes.CoverageInfo);
                break;
        }

        // Documentation required -> "Launch SMART application" card that opens DTR
        if (d.DocumentationRequired)
        {
            var appContext = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["coverage-assertion-id"] = d.CoverageAssertionId,
                ["questionnaire"] = d.QuestionnaireUrls,
                ["order"] = order.Reference,
                ["coverage"] = $"Coverage/{coverage.Id}"
            });

            var card = Card(CdsIndicators.Info, Truncate($"Documentation required ({d.DocNeeded}): {what}"),
                $"Complete the payer documentation for {what}. Purpose: {string.Join(", ", d.DocPurpose)}.",
                d.DocPurpose.Contains(DocPurposeCodes.WithClaim) ? CrdCardTypes.Claim : CrdCardTypes.CoverageInfo);
            card.Links = new List<CdsLink>
            {
                new() { Label = "Launch documentation (DTR)", Url = _options.DtrAppLaunchUrl, Type = CdsLinkTypes.Smart, AppContext = appContext }
            };
            yield return card;
        }
    }

    private CdsCard Card(string indicator, string summary, string detail, string cardType, string? link = null)
    {
        var card = new CdsCard
        {
            Summary = summary,
            Detail = detail,
            Indicator = indicator,
            Source = new CdsSource
            {
                Label = _options.PayerName,
                Topic = new CdsCoding { System = CodeSystems.CrdTemp, Code = cardType }
            }
        };
        if (link != null)
            card.Links = new List<CdsLink> { new() { Label = "Coverage policy", Url = link, Type = CdsLinkTypes.Absolute } };
        return card;
    }

    private static string Truncate(string s) => s.Length <= 139 ? s : s[..136] + "...";

    private record HookConfiguration(bool CoverageInfo, int MaxCards);

    /// <summary>Reads extension."davinci-crd.configuration" sent by the EHR.</summary>
    private static HookConfiguration ReadConfiguration(CdsHookRequest request)
    {
        var coverageInfo = true;
        var maxCards = 10;
        if (request.Extension is { ValueKind: JsonValueKind.Object } ext &&
            ext.TryGetProperty(CrdExtensionUrls.Configuration, out var cfg) && cfg.ValueKind == JsonValueKind.Object)
        {
            if (cfg.TryGetProperty(CrdCardTypes.CoverageInfo, out var ci) && ci.ValueKind is JsonValueKind.False) coverageInfo = false;
            if (cfg.TryGetProperty(CrdCardTypes.MaxCards, out var mc) && mc.ValueKind == JsonValueKind.Number && mc.TryGetInt32(out var m)) maxCards = m;
        }
        return new HookConfiguration(coverageInfo, maxCards);
    }
}
