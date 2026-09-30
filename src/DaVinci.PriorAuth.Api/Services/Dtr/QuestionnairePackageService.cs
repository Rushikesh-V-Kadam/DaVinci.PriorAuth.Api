using System.Text.Json;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Crd;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Dtr;

public interface IQuestionnairePackageService
{
    Parameters BuildPackage(Parameters input);
    Questionnaire? FindQuestionnaire(string canonical);
}

/// <summary>
/// DTR $questionnaire-package. Works out which Questionnaires apply, then returns one Bundle per
/// Questionnaire containing the Questionnaire + every CQL Library it needs + the ValueSets it uses.
///
/// Which Questionnaires? In priority order:
///   1. "questionnaire" canonicals sent by the client (from CRD coverage-information / CDex Task)
///   2. "context" = coverage-assertion-id from CRD, or a CDex/PAS tracking id
///   3. evaluate the "order" resources against the coverage rules
/// </summary>
public class QuestionnairePackageService : IQuestionnairePackageService
{
    private readonly IFhirResourceStore _store;
    private readonly ICoverageAssertionRepository _assertions;
    private readonly IPriorAuthRepository _priorAuths;
    private readonly ICoverageRulesEngine _engine;
    private readonly PayerOptions _options;

    public QuestionnairePackageService(IFhirResourceStore store, ICoverageAssertionRepository assertions,
        IPriorAuthRepository priorAuths, ICoverageRulesEngine engine, IOptions<PayerOptions> options)
    {
        _store = store; _assertions = assertions; _priorAuths = priorAuths; _engine = engine; _options = options.Value;
    }

    public Parameters BuildPackage(Parameters input)
    {
        var output = (Parameters)new Parameters().WithProfile(DtrProfiles.QPackageOutputParameters);
        var outcome = new OperationOutcome();

        var coverage = input.Get(DtrParams.Coverage).Select(p => p.Resource).OfType<Coverage>().FirstOrDefault();
        var orders = input.Get(DtrParams.Order).Select(p => p.Resource).Where(r => r != null).Cast<Resource>().ToList();
        var context = input.GetSingle(DtrParams.Context)?.ValueString;
        var changedSince = FhirDate.TryParse(input.GetSingle(DtrParams.ChangedSince)?.ValueDateTime);

        // 1. explicit canonicals
        var canonicals = input.Get(DtrParams.Questionnaire)
            .Select(p => p.ValueCanonical).Where(c => !string.IsNullOrEmpty(c)).Cast<string>().ToList();

        // 2. context -> CRD coverage assertion or PAS/CDex tracking id
        if (context != null)
        {
            var assertion = _assertions.Get(context);
            if (assertion != null) canonicals.AddRange(assertion.Decision.QuestionnaireUrls);

            var priorAuth = _priorAuths.FindByTrackingId(context);
            if (priorAuth != null) canonicals.AddRange(priorAuth.Items.SelectMany(i => i.MissingQuestionnaires));
        }

        // 3. evaluate orders against the rules
        if (canonicals.Count == 0 && orders.Count > 0)
        {
            foreach (var order in orders)
            {
                var rule = _engine.FindRule(coverage, OrderCodes.From(order));
                if (rule?.DocNeeded != null) canonicals.AddRange(rule.QuestionnaireUrls);
            }
        }

        canonicals = canonicals.Distinct().ToList();
        if (canonicals.Count == 0)
        {
            outcome.Add(IssueSeverity.Information, IssueType.Informational,
                "No documentation requirements were found for the supplied coverage/orders/context.");
            output.AddResource(DtrParams.Outcome, outcome);
            return output;
        }

        foreach (var canonical in canonicals)
        {
            var questionnaire = FindQuestionnaire(canonical);
            if (questionnaire == null)
            {
                outcome.Add(IssueSeverity.Warning, IssueType.NotFound, $"Questionnaire {canonical} was not found.");
                continue;
            }

            var bundle = BuildBundle(questionnaire, outcome, out var lastChanged);
            if (changedSince != null && lastChanged != null && lastChanged <= changedSince) continue; // unchanged
            output.AddResource(_options.PackageOutputParameterName, bundle);
        }

        if (outcome.Issue.Count > 0) output.AddResource(DtrParams.Outcome, outcome);
        return output;
    }

    public Questionnaire? FindQuestionnaire(string canonical)
    {
        var (url, version) = SplitCanonical(canonical);
        var matches = _store.All<Questionnaire>().Where(q => q.Url == url).ToList();
        return version != null
            ? matches.FirstOrDefault(q => q.Version == version)
            : matches.OrderByDescending(q => q.Version).FirstOrDefault();
    }

    private Bundle BuildBundle(Questionnaire questionnaire, OperationOutcome outcome, out DateTimeOffset? lastChanged)
    {
        var bundle = (Bundle)new Bundle { Type = "collection", Timestamp = FhirDate.Now() }.WithProfile(DtrProfiles.QPackageBundle);
        bundle.Add(questionnaire, FullUrl(questionnaire));
        lastChanged = FhirDate.TryParse(questionnaire.Meta?.LastUpdated);

        // Libraries: cqf-library on the Questionnaire + their depends-on chain
        var pending = new Queue<string>(questionnaire.Extension.GetAllByUrl(DtrExtensionUrls.CqfLibrary)
            .Select(e => e.ValueCanonical).Where(c => c != null).Cast<string>());
        var added = new HashSet<string>();
        while (pending.Count > 0)
        {
            var canonical = pending.Dequeue();
            var library = FindLibrary(canonical);
            if (library == null)
            {
                outcome.Add(IssueSeverity.Warning, IssueType.NotFound,
                    $"Library {canonical} needed by {questionnaire.Url} is not loaded (add its .cql/.elm.json to Resources/Cql).");
                continue;
            }
            if (!added.Add(library.Url + "|" + library.Version)) continue;
            bundle.Add(library, FullUrl(library));
            var changed = FhirDate.TryParse(library.Meta?.LastUpdated);
            if (changed != null && (lastChanged == null || changed > lastChanged)) lastChanged = changed;
            foreach (var dep in library.RelatedArtifact.Where(r => r.Type == "depends-on" && r.Resource != null))
                pending.Enqueue(dep.Resource!);
        }

        // ValueSets referenced by answerValueSet
        foreach (var vsUrl in AnswerValueSets(questionnaire.Item).Distinct())
        {
            var (url, version) = SplitCanonical(vsUrl);
            var valueSet = _store.All<ValueSet>().FirstOrDefault(v => v.Url == url && (version == null || v.Version == version));
            if (valueSet != null) bundle.Add(valueSet, FullUrl(valueSet));
        }
        return bundle;
    }

    private Library? FindLibrary(string canonical)
    {
        var (url, version) = SplitCanonical(canonical);
        var matches = _store.All<Library>().Where(l => l.Url == url).ToList();
        return version != null ? matches.FirstOrDefault(l => l.Version == version) : matches.FirstOrDefault();
    }

    private static IEnumerable<string> AnswerValueSets(IEnumerable<QuestionnaireItem> items) =>
        items.SelectMany(i => (i.AnswerValueSet != null ? new[] { i.AnswerValueSet } : Array.Empty<string>())
            .Concat(AnswerValueSets(i.Item)));

    private string FullUrl(Resource r) => $"{_options.BaseUrl}/fhir/{r.ResourceType}/{r.Id}";

    private static (string Url, string? Version) SplitCanonical(string canonical)
    {
        var parts = canonical.Split('|');
        return (parts[0], parts.Length > 1 ? parts[1] : null);
    }
}

/// <summary>Reads billing codes from order resources (typed or generic).</summary>
public static class OrderCodes
{
    public static List<Coding> From(Resource order)
    {
        if (order is not GenericResource g) return new();
        var property = g.ResourceType switch
        {
            CrdOrderTypes.ServiceRequest => "code",
            CrdOrderTypes.DeviceRequest => "codeCodeableConcept",
            CrdOrderTypes.MedicationRequest => "medicationCodeableConcept",
            _ => null
        };
        if (property == null || !g.Data.TryGetValue(property, out var concept) ||
            concept.ValueKind != JsonValueKind.Object || !concept.TryGetProperty("coding", out var codings) ||
            codings.ValueKind != JsonValueKind.Array)
            return new();

        return codings.EnumerateArray()
            .Select(c => new Coding
            {
                System = c.TryGetProperty("system", out var s) ? s.GetString() : null,
                Code = c.TryGetProperty("code", out var v) ? v.GetString() : null
            }).ToList();
    }
}
