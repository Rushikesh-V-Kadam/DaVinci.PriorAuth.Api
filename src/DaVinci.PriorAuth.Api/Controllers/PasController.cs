using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Pas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaVinci.PriorAuth.Controllers;

/// <summary>
/// PAS endpoints.
///   POST /fhir/Claim/$submit       PAS Request Bundle -> PAS Response Bundle
///   POST /fhir/Claim/$inquire      PAS Inquiry Request Bundle -> PAS Inquiry Response Bundle
///   POST /fhir/Subscription        subscribe to pended-result notifications
///   GET  /fhir/Subscription/{id}
///   GET  /fhir/ClaimResponse/{id}
/// </summary>
[ApiController]
[Route("fhir")]
[Authorize(Policy = Policies.PasSubmit)]
public class PasController : ControllerBase
{
    private readonly IPasService _pas;
    private readonly ISubscriptionService _subscriptions;
    private readonly IFhirResourceStore _store;

    public PasController(IPasService pas, ISubscriptionService subscriptions, IFhirResourceStore store)
    {
        _pas = pas; _subscriptions = subscriptions; _store = store;
    }

    [HttpPost("Claim/$submit")]
    public async Task<IActionResult> Submit(CancellationToken ct)
    {
        var (bundle, error) = await FhirRequest.ReadOperationInputAsync<Bundle>(Request, PasOperations.ParamResource);
        if (error != null) return error;
        var result = await _pas.SubmitAsync(bundle!, ct);
        return FhirResults.Fhir(result.Body, result.StatusCode);
    }

    [HttpPost("Claim/$inquire")]
    public async Task<IActionResult> Inquire()
    {
        var (bundle, error) = await FhirRequest.ReadOperationInputAsync<Bundle>(Request, PasOperations.ParamResource);
        if (error != null) return error;
        var result = _pas.Inquire(bundle!);
        return FhirResults.Fhir(result.Body, result.StatusCode);
    }

    [HttpPost("Subscription")]
    public async Task<IActionResult> CreateSubscription()
    {
        var (subscription, error) = await FhirRequest.ReadAsync<Subscription>(Request);
        if (error != null) return error;
        var (created, problem) = _subscriptions.Create(subscription!);
        if (problem != null) return FhirResults.Fhir(problem, 400);
        Response.Headers.Location = $"{Request.Scheme}://{Request.Host}/fhir/Subscription/{created!.Id}";
        return FhirResults.Fhir(created, 201);
    }

    [HttpGet("Subscription/{id}")]
    public IActionResult GetSubscription(string id) =>
        _subscriptions.Get(id) is { } s ? FhirResults.Fhir(s) : FhirResults.Error(404, IssueType.NotFound, $"Subscription/{id} not found.");

    [HttpGet("ClaimResponse/{id}")]
    public IActionResult GetClaimResponse(string id) =>
        _store.Get<ClaimResponse>(id) is { } cr ? FhirResults.Fhir(cr) : FhirResults.Error(404, IssueType.NotFound, $"ClaimResponse/{id} not found.");
}
