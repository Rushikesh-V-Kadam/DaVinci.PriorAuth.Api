#!/usr/bin/env bash
# Runs every CRD / DTR / PAS / CDex scenario against a running API and checks the result.
# Usage:  dotnet run --project src/DaVinci.PriorAuth.Api   (in another terminal)
#         ./tools/run-scenarios.sh [http://localhost:5080]
# Needs: bash, curl, jq  (Windows: use Git Bash or WSL)
set -u
BASE="${1:-http://localhost:5080}"
S="$(cd "$(dirname "$0")/../samples" && pwd)"
PASS=0; FAIL=0

post() { curl -s -X POST "$BASE$1" -H "Content-Type: $2" --data-binary @"$3"; }
check() { # name, actual, expected
  if [ "$2" == "$3" ]; then echo "PASS  $1  ($2)"; PASS=$((PASS+1)); else echo "FAIL  $1  expected=$3 actual=$2"; FAIL=$((FAIL+1)); fi
}
cov() { jq -r '.systemActions[0].resource.extension[] | select(.url|endswith("ext-coverage-information")) | .extension[] | select(.url=="'"$2"'") | (.valueCode // .valueString // .valueCanonical)' <<<"$1" | paste -sd, -; }
action() { jq -r '[.entry[0].resource.item[] | .extension[] | select(.url|endswith("extension-reviewAction")) | .extension[] | select(.url|endswith("reviewActionCode")) | .valueCodeableConcept.coding[0].code] | join(",")' <<<"$1"; }

echo "=== CRD (CDS Hooks) ==="
check "discovery lists 6 hooks" "$(curl -s $BASE/cds-services | jq '.services|length')" "6"
r=$(post /cds-services/crd-order-sign application/json $S/crd-01-order-sign-no-pa-no-doc.json);      check "S1 covered, no PA"          "$(cov "$r" pa-needed)" "no-auth"
r=$(post /cds-services/crd-order-sign application/json $S/crd-02-order-sign-pa-and-clinical-doc.json); check "S2 PA + clinical doc"      "$(cov "$r" pa-needed)/$(cov "$r" doc-needed)" "auth-needed/clinical"
check "S2 DTR launch card (smart link)" "$(jq -r '[.cards[].links[]?|select(.type=="smart")]|length' <<<"$r")" "1"
ASSERTION=$(cov "$r" coverage-assertion-id)
r=$(post /cds-services/crd-order-sign application/json $S/crd-03-order-sign-pa-no-doc.json);          check "S3 PA, no doc"             "$(cov "$r" pa-needed)/$(cov "$r" doc-needed)" "auth-needed/"
r=$(post /cds-services/crd-order-sign application/json $S/crd-04-order-sign-no-pa-doc-for-claim.json); check "S4 doc for claim, no PA"  "$(cov "$r" pa-needed)/$(cov "$r" doc-purpose)" "no-auth/withclaim"
r=$(post /cds-services/crd-order-sign application/json $S/crd-05-order-sign-no-pa-admin-doc.json);    check "S5 admin doc"              "$(cov "$r" doc-needed)" "admin"
r=$(post /cds-services/crd-order-sign application/json $S/crd-06-order-sign-not-covered.json);        check "S6 not covered"            "$(cov "$r" covered)" "not-covered"
r=$(post /cds-services/crd-order-sign application/json $S/crd-07-order-sign-conditional-code.json);   check "S7 conditional (code)"     "$(cov "$r" covered)" "conditional"
r=$(post /cds-services/crd-order-sign application/json $S/crd-08a-order-sign-knee-no-location.json);  check "S8a info-needed location"  "$(cov "$r" pa-needed)/$(cov "$r" info-needed)" "conditional/location"
r=$(post /cds-services/crd-order-sign application/json $S/crd-08b-order-sign-knee-inpatient.json);    check "S8b inpatient knee"        "$(cov "$r" pa-needed)/$(cov "$r" doc-needed)" "auth-needed/both"
r=$(post /cds-services/crd-order-sign application/json $S/crd-08c-order-sign-knee-outpatient.json);   check "S8c outpatient knee"       "$(cov "$r" pa-needed)" "no-auth"
r=$(post /cds-services/crd-order-sign application/json $S/crd-09-order-sign-gold-card.json);          check "S9 gold card"              "$(cov "$r" pa-needed)" "no-auth"
r=$(post /cds-services/crd-order-sign application/json $S/crd-10-order-sign-performpa.json);          check "S10 performpa"             "$(cov "$r" pa-needed)" "performpa"
r=$(post /cds-services/crd-order-sign application/json $S/crd-11-order-sign-no-coverage.json);        check "S11 no coverage card"      "$(jq -r '.cards[0].indicator' <<<"$r")" "warning"
r=$(post /cds-services/crd-order-select application/json $S/crd-12-order-select.json);                check "order-select"              "$(cov "$r" pa-needed)" "auth-needed"
r=$(post /cds-services/crd-appointment-book application/json $S/crd-13-appointment-book.json);        check "appointment-book"          "$(cov "$r" pa-needed)" "auth-needed"
r=$(post /cds-services/crd-order-dispatch application/json $S/crd-14-order-dispatch.json);            check "order-dispatch"            "$(cov "$r" pa-needed)" "auth-needed"

echo "=== DTR ==="
r=$(post '/fhir/Questionnaire/$questionnaire-package' application/fhir+json $S/dtr-01-package-by-questionnaire.json)
check "package by canonical" "$(jq -r '.parameter[]|select(.name=="PackageBundle")|.resource.entry[0].resource.url' <<<"$r")" "http://example.org/fhir/Questionnaire/cpap-e0601"
check "package includes CQL Library" "$(jq -r '[.parameter[]|select(.name=="PackageBundle")|.resource.entry[]|select(.resource.resourceType=="Library")]|length' <<<"$r")" "1"
r=$(post '/fhir/Questionnaire/$questionnaire-package' application/fhir+json $S/dtr-02-package-by-order.json)
check "package by order" "$(jq -r '[.parameter[]|select(.name=="PackageBundle")]|length' <<<"$r")" "1"
sed "s/{{coverage-assertion-id from the CRD response}}/$ASSERTION/" $S/dtr-03-package-by-context.json > /tmp/dtr-03.json
r=$(post '/fhir/Questionnaire/$questionnaire-package' application/fhir+json /tmp/dtr-03.json)
check "package by context (CRD assertion id)" "$(jq -r '[.parameter[]|select(.name=="PackageBundle")]|length' <<<"$r")" "1"
r=$(post '/fhir/Questionnaire/$questionnaire-package' application/fhir+json $S/dtr-04-package-nothing-needed.json)
check "no documentation needed" "$(jq -r '.parameter[0].name' <<<"$r")" "outcome"
r=$(post '/fhir/Questionnaire/$next-question' application/fhir+json $S/dtr-05-next-question-start.json)
check "adaptive: first question" "$(jq -r '.contained[0].item[-1].linkId' <<<"$r")" "spo2"
r=$(post '/fhir/Questionnaire/$next-question' application/fhir+json $S/dtr-06-next-question-after-spo2.json)
check "adaptive: SpO2 91 -> exercise test" "$(jq -r '.contained[0].item[-1].linkId' <<<"$r")" "exercise-test"

echo "=== PAS ==="
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-01-submit-cpap-approved.json);           check "P1 approved"                 "$(action "$r")" "A1"
check "P1 preAuthRef present" "$(jq -r '.entry[0].resource.preAuthRef|startswith("PA")' <<<"$r")" "true"
r2=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-01-submit-cpap-approved.json);          check "P1 duplicate returns same"   "$(jq -r '.entry[0].resource.id' <<<"$r2")" "$(jq -r '.entry[0].resource.id' <<<"$r")"
r=$(post /cds-services/crd-order-sign application/json $S/crd-02-order-sign-pa-and-clinical-doc.json); check "CRD after approval: satisfied" "$(cov "$r" pa-needed)" "satisfied"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-02-submit-cpap-clinical-review.json);    check "P2 pended (clinical review)" "$(action "$r")/$(jq -r '.entry[0].resource.outcome' <<<"$r")" "A4/queued"
P2=$(curl -s $BASE/um/prior-auth | jq -r '.[] | select(.claimIdentifierKey|endswith("PA-CPAP-002")) | .id')
r=$(curl -s -X POST "$BASE/um/prior-auth/$P2/items/1/decision" -H "Content-Type: application/json" -d '{"action":"A3","reasonText":"AHI below 15 without qualifying comorbidity"}')
check "P2 reviewer denial with reason"  "$(action "$r")" "A3"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-03-submit-cpap-missing-questionnaire.json); check "P3 pended, questionnaire needed" "$(action "$r")" "A4"
TASK3=$(curl -s "$BASE/fhir/Task?status=requested" | jq -r '[.entry[].resource | select(.code.coding[0].code=="attachment-request-questionnaire")][0]')
TRACK3=$(jq -r '.identifier[0].value' <<<"$TASK3")
check "P3 Task asks for questionnaire" "$(jq -r '[.input[]|select(.type.coding[0].code=="questionnaire")|.valueCanonical][0]' <<<"$TASK3")" "http://example.org/fhir/Questionnaire/cpap-e0601"
sed "s/{{Task.identifier\[tracking-id\].value}}/$TRACK3/" $S/cdex-02-submit-questionnaire-response.json > /tmp/cdex-02.json
r=$(post '/fhir/$submit-attachment' application/fhir+json /tmp/cdex-02.json);                       check "P3 CDex QR accepted"         "$(jq -r '.issue[0].severity' <<<"$r")" "information"
r=$(post '/fhir/Claim/$inquire' application/fhir+json <(jq '.identifier.value="INQ-3" | .entry[0].resource.identifier[0].value="PA-CPAP-003"' $S/pas-12-inquire.json))
check "P3 approved after DTR answers (inquire)" "$(jq -r '[.entry[0].resource.item[0].extension[]|select(.url|endswith("reviewAction"))|.extension[]|select(.url|endswith("reviewActionCode"))|.valueCodeableConcept.coding[0].code][0]' <<<"$r")" "A1"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-04-submit-mri-no-doc-approved.json);     check "P4 approved (no doc)"        "$(action "$r")" "A1"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-05-submit-mri-partial.json);             check "P5 partial approval"         "$(action "$r")" "A2"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-06-submit-knee-needs-attachment.json);   check "P6 pended, attachment needed" "$(action "$r")" "A4"
TRACK6=$(curl -s "$BASE/fhir/Task?status=requested" | jq -r '[.entry[].resource | select(.code.coding[0].code=="attachment-request-code")][0].identifier[0].value')
sed "s/{{Task.identifier\[tracking-id\].value}}/$TRACK6/" $S/cdex-01-submit-attachment-knee.json > /tmp/cdex-01.json
post '/fhir/$submit-attachment' application/fhir+json /tmp/cdex-01.json > /dev/null
r=$(post '/fhir/Claim/$inquire' application/fhir+json $S/pas-12-inquire.json)
check "P6 approved after CDex attachment" "$(jq -r '[.entry[0].resource.item[0].extension[]|select(.url|endswith("reviewAction"))|.extension[]|select(.url|endswith("reviewActionCode"))|.valueCodeableConcept.coding[0].code][0]' <<<"$r")" "A1"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-07-submit-not-covered.json);             check "P7 not covered -> denied"    "$(action "$r")" "A3"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-08-submit-no-pa-needed.json);            check "P8 no PA needed"             "$(action "$r")" "NA"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-09-submit-multi-line.json);              check "P9 multi-line"               "$(action "$r")" "A1,NA,A3"
r=$(post '/fhir/Claim/$submit' application/fhir+json $S/pas-10-update-cancel-line.json);             check "P10 update cancels line 1"   "$(action "$r")" "C,NA,A3"
code=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/fhir/Claim/\$submit" -H "Content-Type: application/fhir+json" --data-binary @$S/pas-11-submit-invalid.json)
check "P11 invalid bundle -> 400" "$code" "400"
code=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/fhir/Subscription" -H "Content-Type: application/fhir+json" --data-binary @$S/pas-13-subscription.json)
check "P13 subscription created" "$code" "201"

echo; echo "Passed: $PASS   Failed: $FAIL"
[ "$FAIL" -eq 0 ]
