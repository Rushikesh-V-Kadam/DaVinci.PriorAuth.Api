library CpapPrepopulation version '1.0.0'

using FHIR version '4.0.1'

include FHIRHelpers version '4.0.1' called FHIRHelpers

codesystem "CPT": 'http://www.ama-assn.org/go/cpt'
codesystem "ICD10CM": 'http://hl7.org/fhir/sid/icd-10-cm'
// Payer placeholder code system: replace with the LOINC code your provider EHRs use for AHI
codesystem "PayerObservationCodes": 'http://example.org/fhir/CodeSystem/observation-codes'

code "Polysomnography": '95810' from "CPT" display 'Polysomnography, attended'
code "Home sleep test": '95806' from "CPT" display 'Sleep study, unattended'
code "Hypersomnia": 'G47.10' from "ICD10CM" display 'Hypersomnia, unspecified'
code "AHI code": 'ahi' from "PayerObservationCodes" display 'Apnea-hypopnea index'

context Patient

define "SleepStudies":
  ([Procedure: "Polysomnography"] union [Procedure: "Home sleep test"]) P
    where P.status.value = 'completed'

define "SleepStudyDateTime":
  Max("SleepStudies" P return FHIRHelpers.ToDateTime(P.performed as FHIR.dateTime))

define "SleepStudyDate":
  date from "SleepStudyDateTime"

define "LatestAHIObservation":
  Last([Observation: "AHI code"] O
    where O.status.value in { 'final', 'amended', 'corrected' }
    sort by issued)

define "AHI":
  FHIRHelpers.ToDecimal(("LatestAHIObservation".value as FHIR.Quantity).value)

define "HasRecentFaceToFace":
  exists ([Encounter] E
    where E.status.value = 'finished'
      and start of FHIRHelpers.ToInterval(E.period)
        during Interval["SleepStudyDateTime" - 6 months, "SleepStudyDateTime"])

define "HasDaytimeSleepiness":
  exists [Condition: "Hypersomnia"]
