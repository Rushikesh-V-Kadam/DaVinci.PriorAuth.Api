using System.Text.Json;
using System.Text.Json.Nodes;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;

namespace DaVinci.PriorAuth.Services.Crd;

/// <summary>Builds the CRD ext-coverage-information extension from a CoverageDecision.</summary>
public static class CoverageInformationBuilder
{
    public static Extension Build(CoverageDecision d, ResourceReference coverage)
    {
        var ext = new Extension(CrdExtensionUrls.CoverageInformation);
        var parts = ext.Extension_;

        parts.Add(Extension.Ref(CoverageInfoParts.Coverage, coverage));
        parts.Add(Extension.Code(CoverageInfoParts.Covered, d.Covered));
        if (d.PaNeeded != null) parts.Add(Extension.Code(CoverageInfoParts.PaNeeded, d.PaNeeded));
        if (d.DocNeeded != null) parts.Add(Extension.Code(CoverageInfoParts.DocNeeded, d.DocNeeded));
        foreach (var p in d.DocPurpose) parts.Add(Extension.Code(CoverageInfoParts.DocPurpose, p));
        foreach (var i in d.InfoNeeded) parts.Add(Extension.Code(CoverageInfoParts.InfoNeeded, i));
        foreach (var b in d.BillingCodes) parts.Add(new Extension(CoverageInfoParts.BillingCode) { ValueCoding = b });
        foreach (var r in d.Reasons) parts.Add(Extension.Concept(CoverageInfoParts.Reason, r));

        foreach (var detail in d.Details)
        {
            var value = new Extension(CoverageInfoParts.DetailValue)
            {
                ValueBoolean = detail.ValueBoolean,
                ValueQuantity = detail.ValueQuantity,
                ValuePeriod = detail.ValuePeriod,
                ValueString = detail.ValueString
            };
            parts.Add(Extension.Complex(CoverageInfoParts.Detail,
                Extension.Concept(CoverageInfoParts.DetailCategory, new CodeableConcept(CodeSystems.CrdTemp, detail.Category)),
                value));
        }

        foreach (var q in d.QuestionnaireUrls) parts.Add(Extension.Canonical(CoverageInfoParts.Questionnaire, q));
        parts.Add(Extension.Date(CoverageInfoParts.Date, d.Date));
        parts.Add(Extension.String(CoverageInfoParts.CoverageAssertionId, d.CoverageAssertionId));
        if (d.SatisfiedPaId != null) parts.Add(Extension.String(CoverageInfoParts.SatisfiedPaId, d.SatisfiedPaId));
        return ext;
    }

    /// <summary>
    /// Adds the extension to the raw order JSON (echoed back in the systemAction). Any earlier
    /// coverage-information for the same Coverage is replaced, so repeat hook calls do not stack.
    /// </summary>
    public static JsonObject ApplyTo(JsonObject order, Extension coverageInfo)
    {
        var copy = JsonNode.Parse(order.ToJsonString())!.AsObject();
        var coverageRef = coverageInfo.Extension_.First(e => e.Url == CoverageInfoParts.Coverage).ValueReference?.Reference;

        if (copy["extension"] is not JsonArray extensions)
        {
            extensions = new JsonArray();
            copy["extension"] = extensions;
        }

        for (var i = extensions.Count - 1; i >= 0; i--)
        {
            if (extensions[i] is JsonObject e &&
                e["url"]?.GetValue<string>() == CrdExtensionUrls.CoverageInformation &&
                (e["extension"] as JsonArray)?.OfType<JsonObject>().Any(p =>
                    p["url"]?.GetValue<string>() == CoverageInfoParts.Coverage &&
                    p["valueReference"]?["reference"]?.GetValue<string>() == coverageRef) == true)
            {
                extensions.RemoveAt(i);
            }
        }

        var node = JsonSerializer.SerializeToNode(coverageInfo, FhirJson.Compact);
        extensions.Add(node);
        return copy;
    }
}
