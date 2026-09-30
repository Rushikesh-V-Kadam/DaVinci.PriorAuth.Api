using System.Text.Json.Nodes;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Services.Pas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Controllers;

/// <summary>GET /fhir/metadata (CapabilityStatement) and SMART discovery.</summary>
[ApiController]
[AllowAnonymous]
public class MetadataController : ControllerBase
{
    private readonly PayerOptions _options;
    private readonly IConfiguration _config;
    public MetadataController(IOptions<PayerOptions> options, IConfiguration config) { _options = options.Value; _config = config; }

    [HttpGet("fhir/metadata")]
    public IActionResult Metadata()
    {
        JsonObject Op(string name, string canonical) => new() { ["name"] = name, ["definition"] = canonical };

        var cs = new JsonObject
        {
            ["resourceType"] = "CapabilityStatement",
            ["status"] = "active",
            ["date"] = FhirDateNow(),
            ["publisher"] = _options.PayerName,
            ["kind"] = "instance",
            ["fhirVersion"] = IgVersions.FhirVersion,
            ["format"] = new JsonArray("json"),
            ["implementationGuide"] = new JsonArray(
                $"http://hl7.org/fhir/us/davinci-crd/ImplementationGuide/hl7.fhir.us.davinci-crd|{IgVersions.Crd}",
                $"http://hl7.org/fhir/us/davinci-dtr/ImplementationGuide/hl7.fhir.us.davinci-dtr|{IgVersions.Dtr}",
                $"http://hl7.org/fhir/us/davinci-pas/ImplementationGuide/hl7.fhir.us.davinci-pas|{IgVersions.Pas}",
                $"http://hl7.org/fhir/us/davinci-cdex/ImplementationGuide/hl7.fhir.us.davinci-cdex|{IgVersions.Cdex}"),
            ["rest"] = new JsonArray(new JsonObject
            {
                ["mode"] = "server",
                ["security"] = new JsonObject
                {
                    ["service"] = new JsonArray(new JsonObject
                    {
                        ["coding"] = new JsonArray(new JsonObject
                        {
                            ["system"] = "http://terminology.hl7.org/CodeSystem/restful-security-service",
                            ["code"] = "SMART-on-FHIR"
                        })
                    })
                },
                ["resource"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "Claim",
                        ["operation"] = new JsonArray(Op("submit", PasOperations.SubmitCanonical), Op("inquire", PasOperations.InquireCanonical))
                    },
                    new JsonObject { ["type"] = "ClaimResponse", ["interaction"] = new JsonArray(new JsonObject { ["code"] = "read" }) },
                    new JsonObject
                    {
                        ["type"] = "Questionnaire",
                        ["interaction"] = new JsonArray(new JsonObject { ["code"] = "read" }, new JsonObject { ["code"] = "search-type" }),
                        ["operation"] = new JsonArray(
                            Op("questionnaire-package", DtrOperations.QuestionnairePackageCanonical),
                            Op("next-question", "http://hl7.org/fhir/uv/sdc/OperationDefinition/Questionnaire-next-question"))
                    },
                    new JsonObject { ["type"] = "Library", ["interaction"] = new JsonArray(new JsonObject { ["code"] = "read" }) },
                    new JsonObject { ["type"] = "ValueSet", ["interaction"] = new JsonArray(new JsonObject { ["code"] = "read" }) },
                    new JsonObject
                    {
                        ["type"] = "Task",
                        ["interaction"] = new JsonArray(new JsonObject { ["code"] = "read" }, new JsonObject { ["code"] = "update" }, new JsonObject { ["code"] = "search-type" })
                    },
                    new JsonObject
                    {
                        ["type"] = "Subscription",
                        ["interaction"] = new JsonArray(new JsonObject { ["code"] = "create" }, new JsonObject { ["code"] = "read" })
                    }),
                ["operation"] = new JsonArray(Op("submit-attachment", CdexOperations.SubmitAttachmentCanonical))
            })
        };
        return Content(cs.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), MediaTypes.FhirJson);
    }

    /// <summary>SMART configuration so clients can find the payer's token endpoint.</summary>
    [HttpGet(".well-known/smart-configuration")]
    public IActionResult SmartConfiguration() => new JsonResult(new
    {
        issuer = _config["Auth:Authority"],
        token_endpoint = _config["Auth:TokenEndpoint"],
        token_endpoint_auth_methods_supported = new[] { "private_key_jwt" },
        token_endpoint_auth_signing_alg_values_supported = new[] { "RS384", "ES384" },
        grant_types_supported = new[] { "client_credentials", "authorization_code" },
        scopes_supported = new[] { "system/Claim.c", "system/Task.rs", "system/DocumentReference.c", "user/Questionnaire.r", "launch", "openid", "fhirUser" },
        capabilities = new[] { "client-confidential-asymmetric", "launch-ehr", "context-ehr-patient" }
    });

    private static string FhirDateNow() => Models.Fhir.FhirDate.Now();
}

/// <summary>
/// Utilization-management worklist (payer-internal, NOT part of the Da Vinci IGs).
/// A nurse/physician reviewer uses it to decide pended lines; the provider is notified via Subscription.
///   GET  /um/prior-auth                                  pended requests, oldest due date first
///   POST /um/prior-auth/{id}/items/{sequence}/decision   { "action": "A1", "reasonCode": null, "reasonText": "..." }
/// </summary>
[ApiController]
[Route("um/prior-auth")]
public class UmReviewController : ControllerBase
{
    private readonly IPriorAuthRepository _repo;
    private readonly IPasService _pas;
    public UmReviewController(IPriorAuthRepository repo, IPasService pas) { _repo = repo; _pas = pas; }

    [HttpGet]
    public IActionResult Worklist() =>
        Ok(_repo.Pending().OrderBy(r => r.DueAt).Select(r => new
        {
            r.Id, r.ClaimIdentifierKey, r.PatientMemberId, r.Priority, Status = r.Status.ToString(), r.SubmittedAt, r.DueAt, r.TrackingId,
            Items = r.Items.Select(i => new { i.Sequence, i.ProductCode, i.ReviewAction, i.ReasonText, i.MissingAttachments, i.MissingQuestionnaires })
        }));

    public record ReviewerDecision(string Action, string? ReasonCode, string? ReasonText);

    [HttpPost("{id}/items/{sequence:int}/decision")]
    public async Task<IActionResult> Decide(string id, int sequence, [FromBody] ReviewerDecision decision, CancellationToken ct)
    {
        var allowed = new[] { X12ReviewActionCodes.CertifiedInTotal, X12ReviewActionCodes.CertifiedPartial,
                              X12ReviewActionCodes.NotCertified, X12ReviewActionCodes.Modified, X12ReviewActionCodes.ContactPayer };
        if (!allowed.Contains(decision.Action))
            return FhirResults.Error(400, IssueType.Value, $"Action must be one of {string.Join(", ", allowed)}.");

        var result = await _pas.RecordReviewerDecisionAsync(id, sequence, decision.Action, decision.ReasonCode, decision.ReasonText, ct);
        return FhirResults.Fhir(result.Body, result.StatusCode);
    }
}
