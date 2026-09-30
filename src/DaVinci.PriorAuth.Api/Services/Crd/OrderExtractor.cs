using System.Text.Json;
using System.Text.Json.Nodes;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.CdsHooks;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Common;

namespace DaVinci.PriorAuth.Services.Crd;

/// <summary>
/// Pulls the orders (or appointments / encounters) and the patient's Coverage out of a CDS Hooks
/// request. Uses prefetch first and falls back to querying the EHR FHIR server.
/// </summary>
public class OrderExtractor
{
    private readonly EhrFhirClient _ehr;
    public OrderExtractor(EhrFhirClient ehr) { _ehr = ehr; }

    public async Task<List<OrderContext>> GetOrdersAsync(CdsHookRequest hook, CancellationToken ct)
    {
        var all = new List<JsonObject>();   // every resource we can see, used to resolve requester NPI
        var targets = new List<JsonObject>();

        switch (hook.Hook)
        {
            case CrdHooks.OrderSign:
            case CrdHooks.OrderSelect:
            {
                var draft = BundleResources(hook.ContextElement("draftOrders"));
                all.AddRange(draft);
                var selections = StringArray(hook.ContextElement("selections"));
                targets.AddRange(hook.Hook == CrdHooks.OrderSelect && selections.Count > 0
                    ? draft.Where(r => selections.Contains($"{Str(r, "resourceType")}/{Str(r, "id")}"))
                    : draft);
                break;
            }
            case CrdHooks.OrderDispatch:
            {
                var order = PrefetchResource(hook, CrdPrefetchKeys.Order);
                if (order == null && hook.ContextString("order") is { } orderRef)
                {
                    var parts = orderRef.Split('/');
                    if (parts.Length == 2)
                        order = FirstResource(await _ehr.SearchAsync(hook, $"{parts[0]}?_id={parts[1]}", ct));
                }
                if (order != null) targets.Add(order);
                break;
            }
            case CrdHooks.AppointmentBook:
            {
                var appts = BundleResources(hook.ContextElement("appointments"));
                all.AddRange(appts);
                targets.AddRange(appts.Where(a => Str(a, "resourceType") == CrdOrderTypes.Appointment));
                break;
            }
            case CrdHooks.EncounterStart:
            case CrdHooks.EncounterDischarge:
            {
                var encounters = PrefetchBundle(hook, CrdPrefetchKeys.Encounter);
                if (encounters.Count == 0 && hook.ContextString("encounterId") is { } encId)
                    encounters = ToJsonResources(await _ehr.SearchAsync(hook, $"Encounter?_id={encId}", ct));
                targets.AddRange(encounters.Where(e => Str(e, "resourceType") == CrdOrderTypes.Encounter));
                all.AddRange(encounters);
                break;
            }
        }

        // other prefetched resources (practitioners, roles) help resolve the requester NPI
        if (hook.Prefetch != null)
            foreach (var key in hook.Prefetch.Keys)
                all.AddRange(PrefetchBundle(hook, key));

        return targets.Select(t => ToOrderContext(t, all)).ToList();
    }

    /// <summary>Finds the active Coverage for the patient (prefetch "coverage", else EHR query).</summary>
    public async Task<Coverage?> GetCoverageAsync(CdsHookRequest hook, string payerIdentifierValue, CancellationToken ct)
    {
        var coverages = PrefetchBundle(hook, CrdPrefetchKeys.Coverage);
        if (coverages.Count == 0 && hook.ContextString("patientId") is { } patientId)
            coverages = ToJsonResources(await _ehr.SearchAsync(hook, $"Coverage?patient={patientId}&status=active", ct));

        var typed = coverages
            .Where(c => Str(c, "resourceType") == "Coverage")
            .Select(c => FhirJson.Parse<Coverage>(c))
            .Where(c => c != null && c.Status == "active")
            .Cast<Coverage>()
            .ToList();

        // prefer the coverage whose payor is this payer; otherwise the first active one
        return typed.FirstOrDefault(c => c.Payor.Any(p => p.Identifier?.Value == payerIdentifierValue)) ?? typed.FirstOrDefault();
    }

    // ------------------------------------------------------------------ mapping

    private static OrderContext ToOrderContext(JsonObject resource, List<JsonObject> all)
    {
        var type = Str(resource, "resourceType") ?? "";
        var ctx = new OrderContext { ResourceType = type, Id = Str(resource, "id"), Raw = resource };

        switch (type)
        {
            case CrdOrderTypes.ServiceRequest:
                ctx.Codes = Codings(resource["code"]);
                ctx.Quantity = ToDecimal(resource["quantityQuantity"]?["value"]);
                ctx.PlaceOfServiceCode = (resource["locationCode"] as JsonArray)?
                    .SelectMany(lc => Codings(lc)).FirstOrDefault(c => c.System == CodeSystems.PlaceOfService)?.Code;
                ctx.HasPerformer = resource["performer"] is JsonArray { Count: > 0 };
                break;
            case CrdOrderTypes.DeviceRequest:
                ctx.Codes = Codings(resource["codeCodeableConcept"]);
                ctx.HasPerformer = resource["performer"] != null;
                break;
            case CrdOrderTypes.MedicationRequest:
                ctx.Codes = Codings(resource["medicationCodeableConcept"]);
                ctx.Quantity = ToDecimal(resource["dispenseRequest"]?["quantity"]?["value"]);
                break;
            case CrdOrderTypes.Appointment:
                ctx.Codes = (resource["serviceType"] as JsonArray)?.SelectMany(st => Codings(st)).ToList() ?? new();
                break;
            case CrdOrderTypes.Encounter:
                ctx.Codes = (resource["type"] as JsonArray)?.SelectMany(t => Codings(t)).ToList() ?? new();
                break;
        }

        ctx.RequesterNpi = ResolveNpi(resource["requester"], all);
        return ctx;
    }

    private static string? ResolveNpi(JsonNode? reference, List<JsonObject> all, int depth = 0)
    {
        if (reference == null || depth > 2) return null;
        if (reference["identifier"] is JsonObject id && Str(id, "system") == IdentifierSystems.Npi) return Str(id, "value");

        var r = reference["reference"]?.GetValue<string>();
        if (r == null) return null;
        var target = all.FirstOrDefault(x => r.EndsWith($"{Str(x, "resourceType")}/{Str(x, "id")}", StringComparison.Ordinal));
        if (target == null) return null;

        var npi = (target["identifier"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(i => Str(i, "system") == IdentifierSystems.Npi);
        if (npi != null) return Str(npi, "value");

        // PractitionerRole -> Practitioner
        return Str(target, "resourceType") == "PractitionerRole" ? ResolveNpi(target["practitioner"], all, depth + 1) : null;
    }

    // ------------------------------------------------------------------ JSON helpers

    private static List<Coding> Codings(JsonNode? codeableConcept) =>
        (codeableConcept?["coding"] as JsonArray)?.OfType<JsonObject>()
            .Select(c => new Coding { System = Str(c, "system"), Code = Str(c, "code"), Display = Str(c, "display") })
            .ToList() ?? new List<Coding>();

    private static string? Str(JsonNode? node, string name) =>
        node?[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static decimal? ToDecimal(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : null;

    private static List<string> StringArray(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Array } e
            ? e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : new List<string>();

    private static List<JsonObject> BundleResources(JsonElement? bundle)
    {
        if (bundle is not { ValueKind: JsonValueKind.Object } b) return new();
        var node = JsonNode.Parse(b.GetRawText()) as JsonObject;
        return (node?["entry"] as JsonArray)?.OfType<JsonObject>()
            .Select(e => e["resource"] as JsonObject).Where(r => r != null).Cast<JsonObject>().ToList() ?? new();
    }

    private static List<JsonObject> PrefetchBundle(CdsHookRequest hook, string key)
    {
        var element = hook.PrefetchElement(key);
        if (element == null) return new();
        var node = JsonNode.Parse(element.Value.GetRawText()) as JsonObject;
        if (node == null) return new();
        return Str(node, "resourceType") == "Bundle" ? BundleResources(element) : new List<JsonObject> { node };
    }

    private static JsonObject? PrefetchResource(CdsHookRequest hook, string key) =>
        PrefetchBundle(hook, key).FirstOrDefault();

    private static List<JsonObject> ToJsonResources(Bundle? bundle) =>
        bundle == null ? new() : bundle.Entry.Where(e => e.Resource != null).Select(e => FhirJson.ToNode(e.Resource!)).ToList();

    private static JsonObject? FirstResource(Bundle? bundle) => ToJsonResources(bundle).FirstOrDefault();
}
