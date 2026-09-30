library DialysisPrepopulation version '1.0.0'

// Pre-population logic shared by the dialysis questionnaires (in-center hemodialysis,
// home dialysis, AKI dialysis, ESA, IV iron, secondary hyperparathyroidism). A DTR SMART app runs this CQL against the
// provider's FHIR server to pre-fill answers. The Payer Gateway form does not run CQL yet:
// the user types the answers (values are visible in the EHR chart).

using FHIR version '4.0.1'

include FHIRHelpers version '4.0.1' called FHIRHelpers

codesystem "LOINC": 'http://loinc.org'
codesystem "ICD10CM": 'http://hl7.org/fhir/sid/icd-10-cm'

code "eGFR MDRD": '33914-3' from "LOINC" display 'Glomerular filtration rate/1.73 sq M.predicted (MDRD)'
code "eGFR CKD-EPI 2021": '98979-8' from "LOINC" display 'Glomerular filtration rate/1.73 sq M.predicted (CKD-EPI 2021)'
code "Hemoglobin": '718-7' from "LOINC" display 'Hemoglobin [Mass/volume] in Blood'
code "Ferritin": '2276-4' from "LOINC" display 'Ferritin [Mass/volume] in Serum or Plasma'
code "Iron saturation": '2502-3' from "LOINC" display 'Iron saturation [Mass Fraction] in Serum or Plasma'
code "Albumin": '1751-7' from "LOINC" display 'Albumin [Mass/volume] in Serum or Plasma'
code "Creatinine": '2160-0' from "LOINC" display 'Creatinine [Mass/volume] in Serum or Plasma'
code "Intact PTH": '2731-8' from "LOINC" display 'Parathyrin.intact [Mass/volume] in Serum or Plasma'
code "Calcium": '17861-6' from "LOINC" display 'Calcium [Mass/volume] in Serum or Plasma'
code "Phosphate": '2777-1' from "LOINC" display 'Phosphate [Mass/volume] in Serum or Plasma'

code "ESRD": 'N18.6' from "ICD10CM" display 'End stage renal disease'
code "CKD stage 5": 'N18.5' from "ICD10CM" display 'Chronic kidney disease, stage 5'
code "Dialysis dependence": 'Z99.2' from "ICD10CM" display 'Dependence on renal dialysis'

context Patient

// ------------------------------------------------------------------ helpers

define function "Final"(obs List<Observation>):
  obs O where O.status.value in { 'final', 'amended', 'corrected' }

define function "Latest"(obs List<Observation>):
  Last("Final"(obs) O sort by (FHIRHelpers.ToDateTime(effective as FHIR.dateTime)))

define function "NumberOf"(obs Observation):
  FHIRHelpers.ToDecimal((obs.value as FHIR.Quantity).value)

define function "DateOf"(obs Observation):
  date from FHIRHelpers.ToDateTime(obs.effective as FHIR.dateTime)

// ------------------------------------------------------------------ kidney function

define "LatestEgfrObservation":
  "Latest"([Observation: "eGFR MDRD"] union [Observation: "eGFR CKD-EPI 2021"])

define "LatestEgfr":
  "NumberOf"("LatestEgfrObservation")

define "LatestEgfrDate":
  "DateOf"("LatestEgfrObservation")

define "LatestCreatinine":
  "NumberOf"("Latest"([Observation: "Creatinine"]))

define "KidneyConditions":
  ([Condition: "ESRD"] union [Condition: "CKD stage 5"]) C
    where FHIRHelpers.ToConcept(C.clinicalStatus).codes[0].code = 'active'

define "PrimaryDiagnosisCode":
  First("KidneyConditions" C return C.code.coding[0].code.value)

define "EsrdOnsetDate":
  Min("KidneyConditions" C return date from FHIRHelpers.ToDateTime(C.onset as FHIR.dateTime))

// ------------------------------------------------------------------ anemia and nutrition labs

define "LatestHemoglobin":
  "NumberOf"("Latest"([Observation: "Hemoglobin"]))

define "LatestHemoglobinDate":
  "DateOf"("Latest"([Observation: "Hemoglobin"]))

define "LatestFerritin":
  "NumberOf"("Latest"([Observation: "Ferritin"]))

define "LatestTsat":
  "NumberOf"("Latest"([Observation: "Iron saturation"]))

define "LatestAlbumin":
  "NumberOf"("Latest"([Observation: "Albumin"]))

// ------------------------------------------------------------------ mineral and bone disorder labs

define "LatestPth":
  "NumberOf"("Latest"([Observation: "Intact PTH"]))

define "LatestPthDate":
  "DateOf"("Latest"([Observation: "Intact PTH"]))

define "LatestCalcium":
  "NumberOf"("Latest"([Observation: "Calcium"]))

define "LatestPhosphorus":
  "NumberOf"("Latest"([Observation: "Phosphate"]))
