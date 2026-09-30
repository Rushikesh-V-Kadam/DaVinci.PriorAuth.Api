using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.Fhir;

namespace DaVinci.PriorAuth.Services.Dtr;

public interface IAdaptiveQuestionnaireService
{
    QuestionnaireResponse NextQuestion(QuestionnaireResponse response, out OperationOutcome? error);
}

/// <summary>
/// SDC $next-question for DTR adaptive forms. The client sends the QuestionnaireResponse with the
/// adaptive Questionnaire contained in it; we append the next applicable question (one at a time)
/// to the contained Questionnaire. When no questions are left, status is set to "completed".
///
/// Question logic lives in a payer-internal "template" (Resources/Adaptive/*.template.json):
/// the full item list with enableWhen rules. Clients never see the template.
/// </summary>
public class AdaptiveQuestionnaireService : IAdaptiveQuestionnaireService
{
    private readonly Dictionary<string, Questionnaire> _templates = new();

    public AdaptiveQuestionnaireService(IWebHostEnvironment env)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Resources", "Adaptive");
        if (!Directory.Exists(dir)) dir = Path.Combine(env.ContentRootPath, "Resources", "Adaptive");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.template.json"))
        {
            if (FhirJson.Parse(File.ReadAllText(file)) is Questionnaire q && q.Url != null) _templates[q.Url] = q;
        }
    }

    public QuestionnaireResponse NextQuestion(QuestionnaireResponse response, out OperationOutcome? error)
    {
        error = null;
        var contained = response.Contained.OfType<Questionnaire>().FirstOrDefault();
        if (contained?.Url == null || !_templates.TryGetValue(contained.Url, out var template))
        {
            error = OperationOutcome.Create(IssueSeverity.Error, IssueType.NotFound,
                "QuestionnaireResponse must contain the adaptive Questionnaire (with its url) returned by $questionnaire-package.");
            return response;
        }

        // Wait until the last question asked has been answered
        var lastAsked = contained.Item.LastOrDefault();
        if (lastAsked != null && lastAsked.Required == true && !IsAnswered(response, lastAsked.LinkId))
            return response;

        foreach (var item in template.Item)
        {
            if (contained.Item.Any(i => i.LinkId == item.LinkId)) continue;
            if (!IsEnabled(item, response)) continue;

            contained.Item.Add(item);   // ask this question next
            response.Status = "in-progress";
            return response;
        }

        response.Status = "completed";   // nothing left to ask
        return response;
    }

    private static bool IsAnswered(QuestionnaireResponse qr, string linkId) =>
        qr.FindItem(linkId)?.Answer.Count > 0;

    /// <summary>Evaluates enableWhen (behavior "all" by default, "any" if set).</summary>
    private static bool IsEnabled(QuestionnaireItem item, QuestionnaireResponse qr)
    {
        if (item.EnableWhen.Count == 0) return true;
        var results = item.EnableWhen.Select(ew => Evaluate(ew, qr.FindItem(ew.Question)?.Answer.FirstOrDefault()));
        return item.EnableBehavior == "any" ? results.Any(r => r) : results.All(r => r);
    }

    private static bool Evaluate(QuestionnaireEnableWhen ew, QuestionnaireResponseAnswer? answer)
    {
        if (ew.Operator == "exists") return (answer != null) == (ew.AnswerBoolean ?? true);
        if (answer == null) return false;

        if (ew.AnswerBoolean != null && answer.ValueBoolean != null)
            return ew.Operator == "=" ? answer.ValueBoolean == ew.AnswerBoolean : answer.ValueBoolean != ew.AnswerBoolean;

        if (ew.AnswerCoding != null && answer.ValueCoding != null)
        {
            var same = ew.AnswerCoding.Code == answer.ValueCoding.Code;
            return ew.Operator == "=" ? same : !same;
        }

        var expected = ew.AnswerDecimal ?? ew.AnswerInteger;
        var actual = answer.ValueDecimal ?? answer.ValueInteger;
        if (expected == null || actual == null) return false;
        return ew.Operator switch
        {
            "=" => actual == expected,
            "!=" => actual != expected,
            ">" => actual > expected,
            "<" => actual < expected,
            ">=" => actual >= expected,
            "<=" => actual <= expected,
            _ => false
        };
    }
}
