using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Cdex;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaVinci.PriorAuth.Controllers;

/// <summary>
/// CDex endpoints.
///   POST /fhir/$submit-attachment          provider sends documents / QuestionnaireResponses
///   GET  /fhir/Task/{id}                   read an additional-information request
///   GET  /fhir/Task?owner=&amp;status=         provider polls for its open requests (pull model)
///   PUT  /fhir/Task/{id}                   provider updates status (received, in-progress, rejected ...)
/// </summary>
[ApiController]
[Route("fhir")]
[Authorize(Policy = Policies.CdexSubmit)]
public class CdexController : ControllerBase
{
    private readonly IAttachmentService _attachments;
    private readonly IFhirResourceStore _store;

    public CdexController(IAttachmentService attachments, IFhirResourceStore store)
    {
        _attachments = attachments; _store = store;
    }

    [HttpPost("$submit-attachment")]
    public async Task<IActionResult> SubmitAttachment(CancellationToken ct)
    {
        var (parameters, error) = await FhirRequest.ReadAsync<Parameters>(Request);
        if (error != null) return error;
        var result = await _attachments.SubmitAsync(parameters!, ct);
        return FhirResults.Fhir(result.Body, result.StatusCode);
    }

    [HttpGet("Task/{id}")]
    public IActionResult GetTask(string id) =>
        _store.Get<FhirTask>(id) is { } t ? FhirResults.Fhir(t) : FhirResults.Error(404, IssueType.NotFound, $"Task/{id} not found.");

    /// <param name="owner">NPI of the provider, e.g. owner=1234567893 (matches Task.owner.identifier.value)</param>
    [HttpGet("Task")]
    public IActionResult SearchTasks([FromQuery] string? owner, [FromQuery] string? status)
    {
        var tasks = _store.All<FhirTask>()
            .Where(t => owner == null || t.Owner?.Identifier?.Value == owner.Split('|').Last())
            .Where(t => status == null || t.Status == status);
        return FhirResults.Fhir(DtrController.SearchSet(tasks));
    }

    [HttpPut("Task/{id}")]
    public async Task<IActionResult> UpdateTask(string id)
    {
        var existing = _store.Get<FhirTask>(id);
        if (existing == null) return FhirResults.Error(404, IssueType.NotFound, $"Task/{id} not found.");
        var (task, error) = await FhirRequest.ReadAsync<FhirTask>(Request);
        if (error != null) return error;

        // The provider may only change status / statusReason / output
        existing.Status = task!.Status;
        existing.StatusReason = task.StatusReason;
        existing.Output = task.Output;
        existing.LastModified = FhirDate.Now();
        return FhirResults.Fhir(_store.Save(existing));
    }
}
