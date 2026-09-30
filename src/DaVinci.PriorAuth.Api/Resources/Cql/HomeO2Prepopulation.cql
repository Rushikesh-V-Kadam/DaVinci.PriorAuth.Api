library HomeO2Prepopulation version '1.0.0'

using FHIR version '4.0.1'

include FHIRHelpers version '4.0.1' called FHIRHelpers

codesystem "LOINC": 'http://loinc.org'

code "SpO2 pulse oximetry": '59408-5' from "LOINC" display 'Oxygen saturation in Arterial blood by Pulse oximetry'
code "SaO2 arterial": '2708-6' from "LOINC" display 'Oxygen saturation in Arterial blood'

context Patient

define "OxygenObservations":
  ([Observation: "SpO2 pulse oximetry"] union [Observation: "SaO2 arterial"]) O
    where O.status.value in { 'final', 'amended', 'corrected' }

define "LatestOxygenObservation":
  Last("OxygenObservations" O sort by issued)

define "LatestSpO2":
  FHIRHelpers.ToDecimal(("LatestOxygenObservation".value as FHIR.Quantity).value)
