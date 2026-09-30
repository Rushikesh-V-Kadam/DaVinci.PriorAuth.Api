library KneePrepopulation version '1.0.0'

using FHIR version '4.0.1'

include FHIRHelpers version '4.0.1' called FHIRHelpers

codesystem "LOINC": 'http://loinc.org'
codesystem "CPT": 'http://www.ama-assn.org/go/cpt'

code "BMI": '39156-5' from "LOINC" display 'Body mass index (BMI) [Ratio]'
code "Therapeutic exercise": '97110' from "CPT" display 'Therapeutic exercises'

context Patient

define "PhysicalTherapy":
  [Procedure: "Therapeutic exercise"] P
    where P.status.value = 'completed'

define "FirstTherapyDateTime":
  Min("PhysicalTherapy" P return FHIRHelpers.ToDateTime(P.performed as FHIR.dateTime))

define "ConservativeTherapyWeeks":
  weeks between "FirstTherapyDateTime" and Now()

define "LatestBMIObservation":
  Last([Observation: "BMI"] O
    where O.status.value in { 'final', 'amended', 'corrected' }
    sort by issued)

define "LatestBMI":
  FHIRHelpers.ToDecimal(("LatestBMIObservation".value as FHIR.Quantity).value)
