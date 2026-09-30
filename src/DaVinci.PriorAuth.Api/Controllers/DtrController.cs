using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Dtr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaVinci.PriorAuth.Controllers;

/// <summary>
/// DTR payer endpoints.
///   POST /fhir/Questionnaire/$questionnaire-package   Questionnaire + CQL Libraries + ValueSets
///   POST /fhir/Questionnaire/$next-question           adaptive forms (SDC)
///   GET  /fhir/Questionnaire/{id} | ?url=             read / search
///   GET  /fhir/Library/{id}, /fhir/ValueSet/{id}
/// </summary>
[ApiController]
[Route("fhir")]
[Authorize(Policy = Policies.DtrRead)]
public class DtrController : ControllerBase
{
    private readonly IQuestionnairePackageService _packages;
    private readonly IAdaptiveQuestionnaireService _adaptive;
    private readonly IFhirResourceStore _store;

    public DtrController(IQuestionnairePackageService packages, IAdaptiveQuestionnaireService adaptive, IFhirResourceStore store)
    {
        _packages = packages; _adaptive = adaptive; _store = store;
    }

    [HttpPost("Questionnaire/$questionnaire-package")]
    public async Task<IActionResult> QuestionnairePackage()
    {
        var (input, error) = await FhirRequest.ReadAsync<Parameters>(Request);
        if (error != null) return error;
        return FhirResults.Fhir(_packages.BuildPackage(input!));
    }

    [HttpPost("Questionnaire/$next-question")]
    public async Task<IActionResult> NextQuestion()
    {
        // SDC: input parameter "in" (QuestionnaireResponse), output "return"
        var (qr, error) = await FhirRequest.ReadOperationInputAsync<QuestionnaireResponse>(Request, "in");
        if (error != null) return error;
        var result = _adaptive.NextQuestion(qr!, out var outcome);
        return outcome != null ? FhirResults.Fhir(outcome, 400) : FhirResults.Fhir(result);
    }

    [HttpGet("Questionnaire/{id}")]
    public IActionResult ReadQuestionnaire(string id) => Read<Questionnaire>(id);

    [HttpGet("Questionnaire")]
    public IActionResult SearchQuestionnaire([FromQuery] string? url)
    {
        var matches = _store.All<Questionnaire>().Where(q => url == null || q.Url == url.Split('|')[0]).ToList();
        return FhirResults.Fhir(SearchSet(matches));
    }

    [HttpGet("Library/{id}")]
    public IActionResult ReadLibrary(string id) => Read<Library>(id);

    [HttpGet("ValueSet/{id}")]
    public IActionResult ReadValueSet(string id) => Read<ValueSet>(id);

    private IActionResult Read<T>(string id) where T : Resource =>
        _store.Get<T>(id) is { } r
            ? FhirResults.Fhir(r)
            : FhirResults.Error(404, IssueType.NotFound, $"{typeof(T).Name}/{id} not found.");

    internal static Bundle SearchSet(IEnumerable<Resource> resources)
    {
        var bundle = new Bundle { Type = "searchset", Timestamp = FhirDate.Now() };
        foreach (var r in resources) bundle.Add(r, r.RelativeUrl());
        bundle.Total = bundle.Entry.Count;
        return bundle;
    }
}
