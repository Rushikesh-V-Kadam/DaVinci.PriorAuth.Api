using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Common;

namespace DaVinci.PriorAuth.Services.Pas;

/// <summary>
/// Structural checks for the PAS Request Bundle and PAS Claim profile (PAS 2.0.1).
/// Failures are technical errors -> HTTP 400 with an OperationOutcome (no ClaimResponse).
/// For full profile validation also run the HL7 validator / Firely validator with the PAS package.
/// </summary>
public static class PasRequestValidator
{
    public static OperationOutcome ValidateSubmit(Bundle bundle) => Validate(bundle, inquiry: false);
    public static OperationOutcome ValidateInquiry(Bundle bundle) => Validate(bundle, inquiry: true);

    private static OperationOutcome Validate(Bundle bundle, bool inquiry)
    {
        var oo = new OperationOutcome();
        void Err(string code, string msg, string path) => oo.Add(IssueSeverity.Error, code, msg, path);

        // ---- Bundle (profile-pas-request-bundle)
        if (bundle.Type != "collection") Err(IssueType.Value, "Bundle.type SHALL be 'collection'.", "Bundle.type");
        if (string.IsNullOrEmpty(bundle.Identifier?.Value)) Err(IssueType.Required, "Bundle.identifier is required.", "Bundle.identifier");
        if (string.IsNullOrEmpty(bundle.Timestamp)) Err(IssueType.Required, "Bundle.timestamp is required.", "Bundle.timestamp");
        if (bundle.Entry.Any(e => e.Request != null)) Err(IssueType.Invalid, "Bundle.entry.request is not allowed in a PAS bundle.", "Bundle.entry.request");

        if (bundle.Entry.FirstOrDefault()?.Resource is not Claim claim)
        {
            Err(IssueType.Required, "The first entry of the Bundle SHALL be a Claim.", "Bundle.entry[0].resource");
            return oo;
        }

        var resolver = new BundleResolver(bundle);

        // ---- Claim (profile-claim / profile-claim-inquiry)
        if (claim.Identifier.Count == 0 && !inquiry) Err(IssueType.Required, "Claim.identifier is required.", "Claim.identifier");
        if (claim.Status != "active") Err(IssueType.Value, "Claim.status SHALL be 'active'.", "Claim.status");
        if (claim.Use != "preauthorization") Err(IssueType.Value, "Claim.use SHALL be 'preauthorization'.", "Claim.use");
        if (claim.Type == null) Err(IssueType.Required, "Claim.type is required.", "Claim.type");
        if (string.IsNullOrEmpty(claim.Created)) Err(IssueType.Required, "Claim.created is required.", "Claim.created");
        if (claim.Priority == null) Err(IssueType.Required, "Claim.priority is required.", "Claim.priority");

        if (resolver.Resolve<Patient>(claim.Patient) == null)
            Err(IssueType.Required, "Claim.patient must reference a Patient in the Bundle.", "Claim.patient");
        if (!resolver.CanResolve(claim.Insurer))
            Err(IssueType.Required, "Claim.insurer must reference an Organization in the Bundle.", "Claim.insurer");
        if (!resolver.CanResolve(claim.Provider))
            Err(IssueType.Required, "Claim.provider must reference an Organization or PractitionerRole in the Bundle.", "Claim.provider");

        if (claim.Insurance.Count == 0)
            Err(IssueType.Required, "Claim.insurance is required (1..*).", "Claim.insurance");
        else
        {
            if (!claim.Insurance.Any(i => i.Focal)) Err(IssueType.Value, "One Claim.insurance SHALL have focal = true.", "Claim.insurance.focal");
            for (var i = 0; i < claim.Insurance.Count; i++)
                if (resolver.Resolve<Coverage>(claim.Insurance[i].Coverage) == null)
                    Err(IssueType.Required, "Claim.insurance.coverage must reference a Coverage in the Bundle.", $"Claim.insurance[{i}].coverage");
        }

        if (claim.Item.Count == 0)
            Err(IssueType.Required, "Claim.item is required (1..*).", "Claim.item");

        for (var i = 0; i < claim.Item.Count; i++)
        {
            var item = claim.Item[i];
            if (item.Sequence <= 0) Err(IssueType.Required, "Claim.item.sequence is required.", $"Claim.item[{i}].sequence");
            if (item.ProductOrService?.Coding.Count is null or 0)
                Err(IssueType.Required, "Claim.item.productOrService is required.", $"Claim.item[{i}].productOrService");
            if (!inquiry && item.Category?.Coding.Count is null or 0)
                Err(IssueType.Required, "Claim.item.category (X12 service type) is required.", $"Claim.item[{i}].category");
        }

        if (claim.Item.Select(i => i.Sequence).Distinct().Count() != claim.Item.Count)
            Err(IssueType.Invalid, "Claim.item.sequence values must be unique.", "Claim.item.sequence");

        return oo;
    }
}
