using System.Text.Json.Serialization;

namespace DaVinci.PriorAuth.Models.Fhir;

public class Questionnaire : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Questionnaire";
    public string? Url { get; set; }
    public List<Identifier> Identifier { get; set; } = new();
    public string? Version { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "active";
    public List<string> SubjectType { get; set; } = new();
    public string? Date { get; set; }
    public string? Publisher { get; set; }
    public string? Description { get; set; }
    public List<Coding> Code { get; set; } = new();
    public List<QuestionnaireItem> Item { get; set; } = new();

    /// <summary>Version-specific canonical: url|version</summary>
    public string VersionedCanonical() => Version == null ? Url ?? "" : $"{Url}|{Version}";
}

public class QuestionnaireItem : BackboneElement
{
    public string LinkId { get; set; } = string.Empty;
    public string? Definition { get; set; }
    public List<Coding> Code { get; set; } = new();
    public string? Prefix { get; set; }
    public string? Text { get; set; }
    /// <summary>group | display | boolean | decimal | integer | date | dateTime | string | text | choice | open-choice | attachment | reference | quantity</summary>
    public string Type { get; set; } = "string";
    public List<QuestionnaireEnableWhen> EnableWhen { get; set; } = new();
    public string? EnableBehavior { get; set; }
    public bool? Required { get; set; }
    public bool? Repeats { get; set; }
    public bool? ReadOnly { get; set; }
    public string? AnswerValueSet { get; set; }
    public List<QuestionnaireAnswerOption> AnswerOption { get; set; } = new();
    /// <summary>Default answer(s) shown before the user answers (e.g. 3 treatments per week).</summary>
    public List<QuestionnaireItemInitial> Initial { get; set; } = new();
    public List<QuestionnaireItem> Item { get; set; } = new();
}

/// <summary>Questionnaire.item.initial: one default value (only one value[x] is set).</summary>
public class QuestionnaireItemInitial
{
    public bool? ValueBoolean { get; set; }
    public decimal? ValueDecimal { get; set; }
    public int? ValueInteger { get; set; }
    public string? ValueDate { get; set; }
    public string? ValueString { get; set; }
    public Coding? ValueCoding { get; set; }
}

public class QuestionnaireEnableWhen
{
    public string Question { get; set; } = string.Empty;
    /// <summary>exists | = | != | &gt; | &lt; | &gt;= | &lt;=</summary>
    public string Operator { get; set; } = "=";
    public bool? AnswerBoolean { get; set; }
    public decimal? AnswerDecimal { get; set; }
    public int? AnswerInteger { get; set; }
    public string? AnswerString { get; set; }
    public Coding? AnswerCoding { get; set; }
}

public class QuestionnaireAnswerOption
{
    public Coding? ValueCoding { get; set; }
    public string? ValueString { get; set; }
    public int? ValueInteger { get; set; }
    public bool? InitialSelected { get; set; }
}

public class QuestionnaireResponse : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "QuestionnaireResponse";
    public Identifier? Identifier { get; set; }
    public List<ResourceReference> BasedOn { get; set; } = new();
    public string? Questionnaire { get; set; }
    /// <summary>in-progress | completed | amended | entered-in-error | stopped</summary>
    public string Status { get; set; } = "in-progress";
    public ResourceReference? Subject { get; set; }
    public string? Authored { get; set; }
    public ResourceReference? Author { get; set; }
    public List<QuestionnaireResponseItem> Item { get; set; } = new();

    /// <summary>Depth-first search for an item by linkId.</summary>
    public QuestionnaireResponseItem? FindItem(string linkId) => Find(Item, linkId);

    private static QuestionnaireResponseItem? Find(IEnumerable<QuestionnaireResponseItem> items, string linkId)
    {
        foreach (var i in items)
        {
            if (i.LinkId == linkId) return i;
            var nested = Find(i.Item, linkId) ?? Find(i.Answer.SelectMany(a => a.Item), linkId);
            if (nested != null) return nested;
        }
        return null;
    }
}

public class QuestionnaireResponseItem : BackboneElement
{
    public string LinkId { get; set; } = string.Empty;
    public string? Definition { get; set; }
    public string? Text { get; set; }
    public List<QuestionnaireResponseAnswer> Answer { get; set; } = new();
    public List<QuestionnaireResponseItem> Item { get; set; } = new();
}

public class QuestionnaireResponseAnswer : BackboneElement
{
    public bool? ValueBoolean { get; set; }
    public decimal? ValueDecimal { get; set; }
    public int? ValueInteger { get; set; }
    public string? ValueDate { get; set; }
    public string? ValueDateTime { get; set; }
    public string? ValueString { get; set; }
    public Coding? ValueCoding { get; set; }
    public Quantity? ValueQuantity { get; set; }
    public ResourceReference? ValueReference { get; set; }
    public List<QuestionnaireResponseItem> Item { get; set; } = new();
}

public class Library : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "Library";
    public string? Url { get; set; }
    public string? Version { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "active";
    public CodeableConcept Type { get; set; } = new("http://terminology.hl7.org/CodeSystem/library-type", "logic-library");
    public List<RelatedArtifact> RelatedArtifact { get; set; } = new();
    public List<DataRequirement> DataRequirement { get; set; } = new();
    public List<Attachment> Content { get; set; } = new();
}

public class RelatedArtifact
{
    /// <summary>documentation | justification | citation | predecessor | successor | derived-from | depends-on | composed-of</summary>
    public string Type { get; set; } = "depends-on";
    public string? Display { get; set; }
    public string? Resource { get; set; }
}

public class DataRequirement
{
    public string Type { get; set; } = string.Empty;
    public List<string> Profile { get; set; } = new();
}

public class ValueSet : DomainResource
{
    [JsonPropertyOrder(-100)] public override string ResourceType => "ValueSet";
    public string? Url { get; set; }
    public string? Version { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "active";
    public ValueSetExpansion? Expansion { get; set; }
}

public class ValueSetExpansion
{
    public string? Identifier { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public List<ValueSetContains> Contains { get; set; } = new();
}

public class ValueSetContains
{
    public string? System { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }
}
