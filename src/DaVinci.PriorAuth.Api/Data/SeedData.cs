using System.Text;
using System.Text.RegularExpressions;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;

namespace DaVinci.PriorAuth.Data;

/// <summary>
/// Loads the coverage policy table, Questionnaires, CQL Libraries and ValueSets at startup.
/// Every CRD / DTR / PAS scenario in the guide is driven by one of these rows.
/// </summary>
public static class SeedData
{
    public const string QCpap = "http://example.org/fhir/Questionnaire/cpap-e0601";
    public const string QHomeO2 = "http://example.org/fhir/Questionnaire/home-o2-adaptive";
    public const string QCrutches = "http://example.org/fhir/Questionnaire/crutches-e0114";
    public const string QKnee = "http://example.org/fhir/Questionnaire/knee-arthroplasty-27447";
    public const string GoldCardNpi = "1234567893";

    // Dialysis demo questionnaires (Resources/Questionnaires, Resources/Adaptive)
    public const string QInCenterHd = "http://example.org/fhir/Questionnaire/dialysis-incenter-hd";
    public const string QHomeDialysis = "http://example.org/fhir/Questionnaire/home-dialysis-adaptive";
    public const string QEsa = "http://example.org/fhir/Questionnaire/esa-anemia-ckd";
    public const string QAki = "http://example.org/fhir/Questionnaire/aki-dialysis";
    public const string QIron = "http://example.org/fhir/Questionnaire/iron-therapy-ckd";
    public const string QShpt = "http://example.org/fhir/Questionnaire/shpt-calcimimetic";

    public static void Load(IServiceProvider services, string contentRoot)
    {
        var rules = services.GetRequiredService<ICoverageRuleRepository>();
        foreach (var rule in CoverageRules()) rules.Add(rule);

        var store = services.GetRequiredService<IFhirResourceStore>();
        var resourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        if (!Directory.Exists(resourceDir)) resourceDir = Path.Combine(contentRoot, "Resources");

        // Questionnaires and ValueSets (JSON files)
        foreach (var file in Directory.GetFiles(Path.Combine(resourceDir, "Questionnaires"), "*.json"))
        {
            var resource = FhirJson.Parse(File.ReadAllText(file));
            if (resource != null) store.Save(resource);
        }

        // CQL libraries: <Name>.cql plus optional compiled <Name>.elm.json
        foreach (var cqlFile in Directory.GetFiles(Path.Combine(resourceDir, "Cql"), "*.cql"))
        {
            store.Save(BuildLibrary(cqlFile));
        }
    }

    /// <summary>Wraps CQL (and ELM if present) into a FHIR Library as DTR requires.</summary>
    public static Library BuildLibrary(string cqlFile)
    {
        var cql = File.ReadAllText(cqlFile);
        var header = Regex.Match(cql, @"library\s+""?(\w+)""?\s+version\s+'([^']+)'");
        var name = header.Success ? header.Groups[1].Value : Path.GetFileNameWithoutExtension(cqlFile);
        var version = header.Success ? header.Groups[2].Value : "1.0.0";

        var library = new Library
        {
            Id = name,
            Url = LibraryCanonical(name),
            Version = version,
            Name = name,
            Title = name,
            Status = "active",
        };
        library.Content.Add(new Attachment { ContentType = MediaTypes.Cql, Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(cql)) });

        var elmFile = Path.ChangeExtension(cqlFile, ".elm.json");
        if (File.Exists(elmFile))
        {
            library.Content.Add(new Attachment { ContentType = MediaTypes.ElmJson, Data = Convert.ToBase64String(File.ReadAllBytes(elmFile)) });
        }

        // include "X" version 'Y' -> relatedArtifact depends-on (so the package can pull dependencies)
        foreach (Match m in Regex.Matches(cql, @"include\s+""?(\w+)""?\s+version\s+'([^']+)'"))
        {
            library.RelatedArtifact.Add(new RelatedArtifact
            {
                Type = "depends-on",
                Resource = $"{LibraryCanonical(m.Groups[1].Value)}|{m.Groups[2].Value}"
            });
        }
        return library;
    }

    /// <summary>FHIRHelpers keeps its HL7 canonical; payer libraries use the payer's base.</summary>
    public static string LibraryCanonical(string name) =>
        name == "FHIRHelpers" ? "http://hl7.org/fhir/Library/FHIRHelpers" : $"http://example.org/fhir/Library/{name}";

    /// <summary>The payer policy table. One row per scenario in the guide.</summary>
    public static IEnumerable<CoverageRule> CoverageRules() => new[]
    {
        // S1: covered, no PA, no documentation
        new CoverageRule { RuleId = "R-99213", CodeSystem = CodeSystems.Cpt, Code = "99213",
            Description = "Office visit, established patient", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.NoAuth,
            X12ServiceType = X12ServiceTypes.MedicalCare },

        // S2: PA required + clinical documentation (DTR) -> PAS with QuestionnaireResponse
        new CoverageRule { RuleId = "R-E0601", CodeSystem = CodeSystems.Hcpcs, Code = "E0601",
            Description = "CPAP device", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa },
            QuestionnaireUrls = { QCpap }, AllowedQuantity = 1, AllowedPeriodMonths = 13,
            X12ServiceType = X12ServiceTypes.DmeRental, PolicyUrl = "https://example.org/policies/cpap",
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "ahi", Operator = ">=", Number = 15, Description = "AHI >= 15 events/hour" },
                new ApprovalCriterion { LinkId = "face-to-face", Operator = "true", Description = "Face-to-face evaluation documented" }
            } },

        // S3: PA required, no documentation -> PAS directly
        new CoverageRule { RuleId = "R-72148", CodeSystem = CodeSystems.Cpt, Code = "72148",
            Description = "MRI lumbar spine without contrast", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            AllowedQuantity = 1, X12ServiceType = X12ServiceTypes.DiagnosticXRay },

        // S4: no PA, clinical documentation needed for the claim (adaptive questionnaire)
        new CoverageRule { RuleId = "R-E1390", CodeSystem = CodeSystems.Hcpcs, Code = "E1390",
            Description = "Oxygen concentrator", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.NoAuth,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithClaim },
            QuestionnaireUrls = { QHomeO2 }, X12ServiceType = X12ServiceTypes.DmeRental },

        // S5: no PA, administrative documentation only
        new CoverageRule { RuleId = "R-E0114", CodeSystem = CodeSystems.Hcpcs, Code = "E0114",
            Description = "Crutches, underarm", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.NoAuth,
            DocNeeded = DocNeededCodes.Admin, DocPurpose = { DocPurposeCodes.WithOrder },
            QuestionnaireUrls = { QCrutches }, X12ServiceType = X12ServiceTypes.DmePurchase },

        // S6: not covered
        new CoverageRule { RuleId = "R-15780", CodeSystem = CodeSystems.Cpt, Code = "15780",
            Description = "Dermabrasion, total face", Covered = CoveredCodes.NotCovered,
            NotCoveredReason = "Cosmetic procedures are excluded from the member's benefit plan.",
            X12ServiceType = X12ServiceTypes.Surgical },

        // S7: code too generic -> conditional + detail-code
        new CoverageRule { RuleId = "R-64999", CodeSystem = CodeSystems.Cpt, Code = "64999",
            Description = "Unlisted procedure, nervous system", Covered = CoveredCodes.Conditional,
            PaNeeded = PaNeededCodes.Conditional, RequiresMoreSpecificCode = true, X12ServiceType = X12ServiceTypes.Surgical },

        // S8: PA depends on place of service; clinical + admin docs; attachment (imaging report) needed -> CDex
        new CoverageRule { RuleId = "R-27447", CodeSystem = CodeSystems.Cpt, Code = "27447",
            Description = "Total knee arthroplasty", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            PaRequiredPlacesOfService = { "21" },   // 21 = inpatient hospital
            DocNeeded = DocNeededCodes.Both, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QKnee },
            RequiredAttachmentLoinc = { LoincCodes.DiagnosticImagingStudy }, X12ServiceType = X12ServiceTypes.Surgical,
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "conservative-therapy-weeks", Operator = ">=", Number = 12, Description = "At least 12 weeks of conservative therapy" },
                new ApprovalCriterion { LinkId = "kl-grade", Operator = ">=", Number = 3, Description = "Kellgren-Lawrence grade 3 or 4" }
            } },

        // S9: PA normally required, waived for gold-carded ordering providers
        new CoverageRule { RuleId = "R-97110", CodeSystem = CodeSystems.Cpt, Code = "97110",
            Description = "Therapeutic exercises (physical therapy)", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            GoldCardNpis = { GoldCardNpi }, AllowedQuantity = 12, X12ServiceType = X12ServiceTypes.PhysicalTherapy },

        // ================================================================ dialysis demo (D1 - D5)

        // D1: monthly ESRD physician services - covered, no PA, no documentation (CRD only)
        new CoverageRule { RuleId = "R-90960", CodeSystem = CodeSystems.Cpt, Code = "90960",
            Description = "ESRD related services, monthly, age 20+, 4 or more visits", Covered = CoveredCodes.Covered,
            PaNeeded = PaNeededCodes.NoAuth, X12ServiceType = X12ServiceTypes.Dialysis },

        // D2: outpatient in-center hemodialysis - PA + questionnaire + lab report attachment (CRD -> DTR -> PAS -> CDex)
        //     39 treatments = 3 per week for 13 weeks; more is approved partially (A2)
        new CoverageRule { RuleId = "R-90935", CodeSystem = CodeSystems.Cpt, Code = "90935",
            Description = "Outpatient in-center hemodialysis", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QInCenterHd },
            RequiredAttachmentLoinc = { LoincCodes.LaboratoryReport }, AllowedQuantity = 39, AllowedPeriodMonths = 3,
            X12ServiceType = X12ServiceTypes.Dialysis, PolicyUrl = "https://example.org/policies/outpatient-dialysis",
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "egfr", Operator = "<=", Number = 15, Description = "eGFR 15 mL/min/1.73 m2 or less" },
                new ApprovalCriterion { LinkId = "cms-2728", Operator = "true", Description = "CMS-2728 Medical Evidence Report completed" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // D3: home peritoneal / home hemodialysis - PA + ADAPTIVE questionnaire ($next-question)
        new CoverageRule { RuleId = "R-90945", CodeSystem = CodeSystems.Cpt, Code = "90945",
            Description = "Home dialysis (peritoneal or home hemodialysis)", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QHomeDialysis },
            AllowedPeriodMonths = 6, X12ServiceType = X12ServiceTypes.Dialysis, PolicyUrl = "https://example.org/policies/home-dialysis",
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "training-completed", Operator = "true", Description = "Home dialysis training completed" },
                new ApprovalCriterion { LinkId = "home-assessment", Operator = "true", Description = "Home assessment completed" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // D4: ESA (epoetin alfa) for anemia of CKD - PA + questionnaire; approved only when the labs meet the criteria,
        //     otherwise pended for clinical review (UM reviewer decides via /um/prior-auth)
        new CoverageRule { RuleId = "R-Q4081", CodeSystem = CodeSystems.Hcpcs, Code = "Q4081",
            Description = "Epoetin alfa for ESRD on dialysis (ESA)", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QEsa },
            AllowedPeriodMonths = 3, X12ServiceType = X12ServiceTypes.MedicalCare, PolicyUrl = "https://example.org/policies/esa",
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "hemoglobin", Operator = "<", Number = 10, Description = "Hemoglobin below 10 g/dL" },
                new ApprovalCriterion { LinkId = "ferritin", Operator = ">=", Number = 200, Description = "Ferritin 200 ng/mL or more (iron replete)" },
                new ApprovalCriterion { LinkId = "tsat", Operator = ">=", Number = 20, Description = "TSAT 20% or more (iron replete)" },
                new ApprovalCriterion { LinkId = "other-causes", Operator = "true", Description = "Other causes of anemia ruled out" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // D5: AV fistula creation - PA required, no documentation (CRD -> PAS)
        new CoverageRule { RuleId = "R-36821", CodeSystem = CodeSystems.Cpt, Code = "36821",
            Description = "Arteriovenous fistula creation (direct anastomosis)", Covered = CoveredCodes.Covered,
            PaNeeded = PaNeededCodes.AuthNeeded, AllowedQuantity = 1, X12ServiceType = X12ServiceTypes.Surgical },

        // ================================================================ codes of the dialysis authorization request form

        // AKI dialysis at an ESRD facility (patient without ESRD) - PA + AKI questionnaire; re-reviewed every month
        new CoverageRule { RuleId = "R-G0491", CodeSystem = CodeSystems.Hcpcs, Code = "G0491",
            Description = "Dialysis for acute kidney injury (without ESRD)", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QAki },
            AllowedQuantity = 13, AllowedPeriodMonths = 1, X12ServiceType = X12ServiceTypes.Dialysis,
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "esrd-ruled-out", Operator = "true", Description = "Patient does not have ESRD" },
                new ApprovalCriterion { LinkId = "recovery-plan", Operator = "true", Description = "Kidney recovery assessment plan documented" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // In-center hemodialysis with repeated evaluations - same questionnaire as 90935, no attachment needed
        new CoverageRule { RuleId = "R-90937", CodeSystem = CodeSystems.Cpt, Code = "90937",
            Description = "Outpatient in-center hemodialysis, repeated evaluation", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QInCenterHd },
            AllowedQuantity = 39, AllowedPeriodMonths = 3, X12ServiceType = X12ServiceTypes.Dialysis,
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "egfr", Operator = "<=", Number = 15, Description = "eGFR 15 mL/min/1.73 m2 or less" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // 90999 used for home hemodialysis (e.g. NxStage) - same adaptive home dialysis questionnaire as 90945
        new CoverageRule { RuleId = "R-90999", CodeSystem = CodeSystems.Cpt, Code = "90999",
            Description = "Home hemodialysis (unlisted dialysis procedure)", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QHomeDialysis },
            AllowedPeriodMonths = 6, X12ServiceType = X12ServiceTypes.Dialysis,
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "training-completed", Operator = "true", Description = "Home dialysis training completed" },
                new ApprovalCriterion { LinkId = "home-assessment", Operator = "true", Description = "Home assessment completed" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // Home dialysis training, completed course - PA, no documentation
        new CoverageRule { RuleId = "R-90989", CodeSystem = CodeSystems.Cpt, Code = "90989",
            Description = "Home dialysis training, completed course", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            AllowedQuantity = 1, X12ServiceType = X12ServiceTypes.Dialysis },

        // Home dialysis training per session (course not completed) - PA, no documentation, at most 25 sessions (more = partial)
        new CoverageRule { RuleId = "R-90993", CodeSystem = CodeSystems.Cpt, Code = "90993",
            Description = "Home dialysis training, per session", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            AllowedQuantity = 25, AllowedPeriodMonths = 3, X12ServiceType = X12ServiceTypes.Dialysis },

        // ESAs of the form (Retacrit, Aranesp, Mircera) - same ESA questionnaire and criteria as Q4081
        EsaRule("R-Q5105", "Q5105", "Retacrit (epoetin alfa-epbx) for ESRD on dialysis"),
        EsaRule("R-J0882", "J0882", "Aranesp (darbepoetin alfa) for ESRD on dialysis"),
        EsaRule("R-J0887", "J0887", "Mircera (methoxy polyethylene glycol-epoetin beta) for ESRD on dialysis"),

        // IV iron sucrose (Venofer) - PA + iron questionnaire; iron is given when TSAT <= 30% and ferritin <= 500
        new CoverageRule { RuleId = "R-J1756", CodeSystem = CodeSystems.Hcpcs, Code = "J1756",
            Description = "Venofer (iron sucrose) injection", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QIron },
            AllowedQuantity = 1000, AllowedPeriodMonths = 3, X12ServiceType = X12ServiceTypes.MedicalCare,
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "tsat", Operator = "<=", Number = 30, Description = "TSAT 30% or less" },
                new ApprovalCriterion { LinkId = "ferritin", Operator = "<=", Number = 500, Description = "Ferritin 500 ng/mL or less" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // Parsabiv (etelcalcetide) for secondary hyperparathyroidism on hemodialysis - PA + questionnaire
        new CoverageRule { RuleId = "R-J0606", CodeSystem = CodeSystems.Hcpcs, Code = "J0606",
            Description = "Parsabiv (etelcalcetide) injection", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QShpt },
            AllowedPeriodMonths = 6, X12ServiceType = X12ServiceTypes.MedicalCare,
            ApprovalCriteria =
            {
                new ApprovalCriterion { LinkId = "ipth", Operator = ">=", Number = 300, Description = "Intact PTH 300 pg/mL or more" },
                new ApprovalCriterion { LinkId = "calcium", Operator = ">=", Number = 8.3m, Description = "Corrected calcium 8.3 mg/dL or more" },
                new ApprovalCriterion { LinkId = "on-hemodialysis", Operator = "true", Description = "Patient on hemodialysis" },
                new ApprovalCriterion { LinkId = "no-cinacalcet", Operator = "true", Description = "Not used together with oral cinacalcet" },
                new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" }
            } },

        // Sensipar (oral cinacalcet) - not a medical benefit of this plan: goes to the pharmacy benefit
        new CoverageRule { RuleId = "R-J0604", CodeSystem = CodeSystems.Hcpcs, Code = "J0604",
            Description = "Sensipar (cinacalcet, oral)", Covered = CoveredCodes.NotCovered,
            NotCoveredReason = "Oral cinacalcet is covered under the member's pharmacy benefit, not the medical benefit. Send the prescription to the pharmacy benefit manager.",
            X12ServiceType = X12ServiceTypes.MedicalCare },

        // Zemplar (paricalcitol) - covered, no PA
        new CoverageRule { RuleId = "R-J2501", CodeSystem = CodeSystems.Hcpcs, Code = "J2501",
            Description = "Zemplar (paricalcitol) injection", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.NoAuth,
            X12ServiceType = X12ServiceTypes.MedicalCare },

        // S10: PA must be requested by the performing (rendering) provider
        new CoverageRule { RuleId = "R-70553", CodeSystem = CodeSystems.Cpt, Code = "70553",
            Description = "MRI brain with and without contrast", Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.PerformPa,
            AllowedQuantity = 1, X12ServiceType = X12ServiceTypes.DiagnosticXRay },
    };

    /// <summary>An ESA rule: PA + ESA questionnaire, approved when the labs meet the anemia criteria (else clinical review).</summary>
    private static CoverageRule EsaRule(string ruleId, string code, string description)
    {
        CoverageRule rule = new CoverageRule
        {
            RuleId = ruleId, CodeSystem = CodeSystems.Hcpcs, Code = code, Description = description,
            Covered = CoveredCodes.Covered, PaNeeded = PaNeededCodes.AuthNeeded,
            DocNeeded = DocNeededCodes.Clinical, DocPurpose = { DocPurposeCodes.WithPa }, QuestionnaireUrls = { QEsa },
            AllowedPeriodMonths = 3, X12ServiceType = X12ServiceTypes.MedicalCare, PolicyUrl = "https://example.org/policies/esa"
        };
        rule.ApprovalCriteria.Add(new ApprovalCriterion { LinkId = "hemoglobin", Operator = "<", Number = 10, Description = "Hemoglobin below 10 g/dL" });
        rule.ApprovalCriteria.Add(new ApprovalCriterion { LinkId = "ferritin", Operator = ">=", Number = 200, Description = "Ferritin 200 ng/mL or more (iron replete)" });
        rule.ApprovalCriteria.Add(new ApprovalCriterion { LinkId = "tsat", Operator = ">=", Number = 20, Description = "TSAT 20% or more (iron replete)" });
        rule.ApprovalCriteria.Add(new ApprovalCriterion { LinkId = "other-causes", Operator = "true", Description = "Other causes of anemia ruled out" });
        rule.ApprovalCriteria.Add(new ApprovalCriterion { LinkId = "attestation", Operator = "true", Description = "Provider attestation" });
        return rule;
    }
}
