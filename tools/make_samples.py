"""Generates every sample request used in the guide into ../samples (run: python3 make_samples.py)."""
import copy, json, os, uuid

OUT = os.path.join(os.path.dirname(__file__), "..", "samples")
os.makedirs(OUT, exist_ok=True)

CPT = "http://www.ama-assn.org/go/cpt"
HCPCS = "https://bluebutton.cms.gov/resources/codesystem/hcpcs"
POS = "https://www.cms.gov/Medicare/Coding/place-of-service-codes/Place_of_Service_Code_Set"
NPI = "http://hl7.org/fhir/sid/us-npi"
X12_1365 = "https://codesystem.x12.org/005010/1365"
X12_1322 = "https://codesystem.x12.org/005010/1322"
X12_1525 = "https://codesystem.x12.org/005010/1525"
PAS = "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/"
EHR = "https://ehr.example.org/fhir"
PAYER_BASE = "http://localhost:5080"
ORDERING_NPI = "1111111112"
GOLD_NPI = "1234567893"
ORG_NPI = "1999999992"


def u(resource_id):
    """Deterministic urn:uuid for a bundle entry (FHIR requires a real UUID after urn:uuid:)."""
    return f"urn:uuid:{uuid.uuid5(uuid.NAMESPACE_URL, 'davinci-sample/' + resource_id)}"


def save(name, obj):
    with open(os.path.join(OUT, name), "w") as f:
        json.dump(obj, f, indent=2)


# ------------------------------------------------------------------ shared resources
patient = {"resourceType": "Patient", "id": "pat-1",
           "identifier": [{"system": "http://example.org/member-ids", "value": "MBR-1001"}],
           "name": [{"family": "Shaw", "given": ["Amy"]}], "gender": "female", "birthDate": "1970-02-20"}

coverage = {"resourceType": "Coverage", "id": "cov-1", "status": "active",
            "subscriberId": "MBR-1001",
            "beneficiary": {"reference": "Patient/pat-1"},
            "relationship": {"coding": [{"system": "http://terminology.hl7.org/CodeSystem/subscriber-relationship", "code": "self"}]},
            "period": {"start": "2026-01-01", "end": "2026-12-31"},
            "payor": [{"identifier": {"system": "http://example.org/payer-ids", "value": "PAYER001"}, "display": "Example Health Plan"}],
            "class": [{"type": {"coding": [{"system": "http://terminology.hl7.org/CodeSystem/coverage-class", "code": "plan"}]},
                       "value": "GOLD-PPO", "name": "Gold PPO"}]}


def practitioner(npi, pid="prac-1"):
    return {"resourceType": "Practitioner", "id": pid,
            "identifier": [{"system": NPI, "value": npi}], "name": [{"family": "Jones", "given": ["Pat"]}]}


def bundle_of(*resources):
    return {"resourceType": "Bundle", "type": "collection",
            "entry": [{"resource": r} for r in resources]}


def service_request(sid, code, system=CPT, display=None, pos=None, requester="Practitioner/prac-1", qty=None):
    sr = {"resourceType": "ServiceRequest", "id": sid, "status": "draft", "intent": "order",
          "code": {"coding": [{"system": system, "code": code, **({"display": display} if display else {})}]},
          "subject": {"reference": "Patient/pat-1"}, "authoredOn": "2026-09-24",
          "requester": {"reference": requester},
          "insurance": [{"reference": "Coverage/cov-1"}]}
    if pos:
        sr["locationCode"] = [{"coding": [{"system": POS, "code": pos}]}]
    if qty:
        sr["quantityQuantity"] = {"value": qty}
    return sr


def device_request(did, code, display):
    return {"resourceType": "DeviceRequest", "id": did, "status": "draft", "intent": "order",
            "codeCodeableConcept": {"coding": [{"system": HCPCS, "code": code, "display": display}]},
            "subject": {"reference": "Patient/pat-1"}, "authoredOn": "2026-09-24",
            "requester": {"reference": "Practitioner/prac-1"},
            "insurance": [{"reference": "Coverage/cov-1"}]}


def hook_request(hook, orders, npi=ORDERING_NPI, with_coverage=True, extra_context=None, draft_key="draftOrders"):
    ctx = {"userId": "Practitioner/prac-1", "patientId": "pat-1", "encounterId": "enc-1"}
    if orders is not None:
        ctx[draft_key] = bundle_of(*orders)
    if extra_context:
        ctx.update(extra_context)
    prefetch = {"patient": patient,
                "coverage": bundle_of(coverage) if with_coverage else bundle_of(),
                "practitioner": bundle_of(practitioner(npi))}
    return {"hook": hook, "hookInstance": str(uuid.UUID(int=len(json.dumps(ctx)))),
            "fhirServer": EHR,
            "context": ctx, "prefetch": prefetch,
            "extension": {"davinci-crd.configuration": {"coverage-info": True, "max-cards": 10}}}


# ------------------------------------------------------------------ CRD samples (one per scenario)
save("crd-01-order-sign-no-pa-no-doc.json", hook_request("order-sign", [service_request("sr-99213", "99213", display="Office visit")]))
save("crd-02-order-sign-pa-and-clinical-doc.json", hook_request("order-sign", [device_request("dr-e0601", "E0601", "CPAP device")]))
save("crd-03-order-sign-pa-no-doc.json", hook_request("order-sign", [service_request("sr-72148", "72148", display="MRI lumbar spine w/o contrast")]))
save("crd-04-order-sign-no-pa-doc-for-claim.json", hook_request("order-sign", [device_request("dr-e1390", "E1390", "Oxygen concentrator")]))
save("crd-05-order-sign-no-pa-admin-doc.json", hook_request("order-sign", [device_request("dr-e0114", "E0114", "Crutches, underarm")]))
save("crd-06-order-sign-not-covered.json", hook_request("order-sign", [service_request("sr-15780", "15780", display="Dermabrasion, total face")]))
save("crd-07-order-sign-conditional-code.json", hook_request("order-sign", [service_request("sr-64999", "64999", display="Unlisted procedure, nervous system")]))
save("crd-08a-order-sign-knee-no-location.json", hook_request("order-sign", [service_request("sr-27447", "27447", display="Total knee arthroplasty")]))
save("crd-08b-order-sign-knee-inpatient.json", hook_request("order-sign", [service_request("sr-27447", "27447", display="Total knee arthroplasty", pos="21")]))
save("crd-08c-order-sign-knee-outpatient.json", hook_request("order-sign", [service_request("sr-27447", "27447", display="Total knee arthroplasty", pos="22")]))
save("crd-09-order-sign-gold-card.json", hook_request("order-sign", [service_request("sr-97110", "97110", display="Therapeutic exercises", qty=12)], npi=GOLD_NPI))
save("crd-10-order-sign-performpa.json", hook_request("order-sign", [service_request("sr-70553", "70553", display="MRI brain w/ and w/o contrast")]))
save("crd-11-order-sign-no-coverage.json", hook_request("order-sign", [service_request("sr-72148", "72148")], with_coverage=False))
save("crd-12-order-select.json", hook_request("order-select", [device_request("dr-e0601", "E0601", "CPAP device")],
                                             extra_context={"selections": ["DeviceRequest/dr-e0601"]}))
appt = {"resourceType": "Appointment", "id": "appt-1", "status": "proposed",
        "serviceType": [{"coding": [{"system": CPT, "code": "72148", "display": "MRI lumbar spine"}]}],
        "participant": [{"actor": {"reference": "Patient/pat-1"}, "status": "accepted"}]}
save("crd-13-appointment-book.json", hook_request("appointment-book", [appt], draft_key="appointments"))
dispatch = hook_request("order-dispatch", None, extra_context={"order": "ServiceRequest/sr-72148", "performer": "Organization/imaging-1"})
dispatch["prefetch"]["order"] = service_request("sr-72148", "72148", display="MRI lumbar spine w/o contrast")
save("crd-14-order-dispatch.json", dispatch)

# ------------------------------------------------------------------ DTR samples
save("dtr-01-package-by-questionnaire.json", {"resourceType": "Parameters",
    "meta": {"profile": ["http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-qpackage-input-parameters"]},
    "parameter": [{"name": "coverage", "resource": coverage},
                  {"name": "questionnaire", "valueCanonical": "http://example.org/fhir/Questionnaire/cpap-e0601|1.0.0"}]})
save("dtr-02-package-by-order.json", {"resourceType": "Parameters",
    "parameter": [{"name": "coverage", "resource": coverage},
                  {"name": "order", "resource": device_request("dr-e0601", "E0601", "CPAP device")}]})
save("dtr-03-package-by-context.json", {"resourceType": "Parameters",
    "parameter": [{"name": "coverage", "resource": coverage},
                  {"name": "context", "valueString": "{{coverage-assertion-id from the CRD response}}"}]})
save("dtr-04-package-nothing-needed.json", {"resourceType": "Parameters",
    "parameter": [{"name": "coverage", "resource": coverage},
                  {"name": "order", "resource": service_request("sr-99213", "99213")}]})

adaptive_q = {"resourceType": "Questionnaire", "id": "home-o2-adaptive",
              "url": "http://example.org/fhir/Questionnaire/home-o2-adaptive", "version": "1.0.0", "status": "active",
              "extension": [{"url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-questionnaireAdaptive", "valueBoolean": True}]}
nq1 = {"resourceType": "QuestionnaireResponse", "id": "qr-o2", "contained": [adaptive_q],
       "questionnaire": "#home-o2-adaptive", "status": "in-progress", "subject": {"reference": "Patient/pat-1"}}
save("dtr-05-next-question-start.json", {"resourceType": "Parameters", "parameter": [{"name": "in", "resource": nq1}]})
nq2 = copy.deepcopy(nq1)
nq2["contained"][0]["item"] = [{"linkId": "spo2", "text": "Resting oxygen saturation (SpO2 %) on room air", "type": "decimal", "required": True}]
nq2["item"] = [{"linkId": "spo2", "answer": [{"valueDecimal": 91,
                "extension": [{"url": "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/information-origin",
                               "extension": [{"url": "source", "valueCode": "auto"}]}]}]}]
save("dtr-06-next-question-after-spo2.json", {"resourceType": "Parameters", "parameter": [{"name": "in", "resource": nq2}]})


def origin(source):
    ext = [{"url": "source", "valueCode": source}]
    if source != "auto":
        ext.append({"url": "author", "extension": [{"url": "practitioner", "valueReference": {"reference": "Practitioner/prac-1"}}]})
    return [{"url": "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/information-origin", "extension": ext}]


def cpap_qr(ahi, f2f=True):
    return {"resourceType": "QuestionnaireResponse", "id": "qr-cpap",
            "meta": {"profile": ["http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-questionnaireresponse"]},
            "questionnaire": "http://example.org/fhir/Questionnaire/cpap-e0601|1.0.0",
            "status": "completed", "subject": {"reference": "Patient/pat-1"}, "authored": "2026-09-24T10:00:00Z",
            "author": {"reference": "Practitioner/prac-1"},
            "item": [
                {"linkId": "sleep-study", "item": [
                    {"linkId": "sleep-study-date", "answer": [{"valueDate": "2026-08-15", "extension": origin("auto")}]},
                    {"linkId": "ahi", "answer": [{"valueDecimal": ahi, "extension": origin("auto")}]}]},
                {"linkId": "face-to-face", "answer": [{"valueBoolean": f2f, "extension": origin("auto")}]},
                {"linkId": "daytime-sleepiness", "answer": [{"valueBoolean": True, "extension": origin("override")}]},
                {"linkId": "attestation", "answer": [{"valueBoolean": True, "extension": origin("manual")}]}]}


def knee_qr(weeks=16, kl=4):
    return {"resourceType": "QuestionnaireResponse", "id": "qr-knee",
            "questionnaire": "http://example.org/fhir/Questionnaire/knee-arthroplasty-27447|1.0.0",
            "status": "completed", "subject": {"reference": "Patient/pat-1"}, "authored": "2026-09-24T10:00:00Z",
            "item": [
                {"linkId": "clinical", "item": [
                    {"linkId": "conservative-therapy-weeks", "answer": [{"valueInteger": weeks, "extension": origin("auto")}]},
                    {"linkId": "kl-grade", "answer": [{"valueInteger": kl, "extension": origin("manual")}]},
                    {"linkId": "bmi", "answer": [{"valueDecimal": 31.2, "extension": origin("auto")}]}]},
                {"linkId": "admin", "item": [
                    {"linkId": "admission-type", "answer": [{"valueCoding": {"system": POS, "code": "21", "display": "Inpatient Hospital"}}]},
                    {"linkId": "planned-date", "answer": [{"valueDate": "2026-11-02"}]}]}]}


# ------------------------------------------------------------------ PAS samples
insurer = {"resourceType": "Organization", "id": "payer-1",
           "identifier": [{"system": "http://example.org/payer-ids", "value": "PAYER001"}], "name": "Example Health Plan"}
requestor = {"resourceType": "Organization", "id": "org-1",
             "identifier": [{"system": NPI, "value": ORG_NPI}], "name": "Good Health Clinic"}


def pas_bundle(bundle_id, claim_id, items, extra=(), priority="normal", related=None, cancel_seq=None):
    claim = {"resourceType": "Claim", "id": "claim-1",
             "meta": {"profile": [PAS + "profile-claim"]},
             "identifier": [{"system": "http://example.org/provider/claim-ids", "value": claim_id}],
             "status": "active",
             "type": {"coding": [{"system": "http://terminology.hl7.org/CodeSystem/claim-type", "code": "professional"}]},
             "use": "preauthorization",
             "patient": {"reference": u("pat-1")},
             "created": "2026-09-24T10:05:00Z",
             "insurer": {"reference": u("payer-1")},
             "provider": {"reference": u("org-1")},
             "priority": {"coding": [{"system": "http://terminology.hl7.org/CodeSystem/processpriority", "code": priority}]},
             "insurance": [{"sequence": 1, "focal": True, "coverage": {"reference": u("cov-1")}}],
             "item": []}
    if related:
        claim["related"] = [{"claim": {"identifier": {"system": "http://example.org/provider/claim-ids", "value": related}},
                             "relationship": {"coding": [{"system": "http://terminology.hl7.org/CodeSystem/ex-relatedclaimrelationship", "code": "prior"}]}}]
    info_seq = 1
    for idx, (code, system, category, qty, pos, service_date) in enumerate(items, start=1):
        cert = "3" if cancel_seq == idx else "I"
        item = {"extension": [
                    {"url": PAS + "extension-itemTraceNumber", "valueIdentifier": {"system": "http://example.org/provider/trace", "value": f"{claim_id}-{idx}"}},
                    {"url": PAS + "extension-certificationType", "valueCodeableConcept": {"coding": [{"system": X12_1322, "code": cert}]}},
                    {"url": PAS + "extension-serviceItemRequestType", "valueCodeableConcept": {"coding": [{"system": X12_1525, "code": "HS", "display": "Health Services Review"}]}}],
                "sequence": idx,
                "category": {"coding": [{"system": X12_1365, "code": category}]},
                "productOrService": {"coding": [{"system": system, "code": code}]},
                "quantity": {"value": qty},
                "servicedDate": service_date}
        if cancel_seq == idx:
            item["modifierExtension"] = [{"url": PAS + "modifierextension-infoCancelled", "valueBoolean": True}]
        if pos:
            item["locationCodeableConcept"] = {"coding": [{"system": POS, "code": pos}]}
        claim["item"].append(item)

    supporting = []
    for r in extra:
        if r["resourceType"] in ("QuestionnaireResponse", "DocumentReference"):
            supporting.append({"sequence": info_seq,
                               "category": {"coding": [{"system": "http://hl7.org/fhir/us/davinci-pas/CodeSystem/PASTempCodes", "code": "additionalInformation"}]},
                               "valueReference": {"reference": u(r['id'])}})
            info_seq += 1
    if supporting:
        claim["supportingInfo"] = supporting

    entries = [claim, patient, coverage, insurer, requestor, practitioner(ORDERING_NPI)] + list(extra)
    return {"resourceType": "Bundle",
            "meta": {"profile": [PAS + "profile-pas-request-bundle"]},
            "identifier": {"system": "http://example.org/provider/bundle-ids", "value": bundle_id},
            "type": "collection", "timestamp": "2026-09-24T10:05:00Z",
            "entry": [{"fullUrl": u(r['id'] if r['resourceType'] != 'Claim' else claim_id), "resource": r} for r in entries]}


save("pas-01-submit-cpap-approved.json", pas_bundle("B-001", "PA-CPAP-001", [("E0601", HCPCS, "18", 1, None, "2026-10-01")], extra=[cpap_qr(22)]))
save("pas-02-submit-cpap-clinical-review.json", pas_bundle("B-002", "PA-CPAP-002", [("E0601", HCPCS, "18", 1, None, "2026-10-01")], extra=[cpap_qr(8)]))
save("pas-03-submit-cpap-missing-questionnaire.json", pas_bundle("B-003", "PA-CPAP-003", [("E0601", HCPCS, "18", 1, None, "2026-10-01")]))
save("pas-04-submit-mri-no-doc-approved.json", pas_bundle("B-004", "PA-MRI-004", [("72148", CPT, "4", 1, None, "2026-10-05")]))
save("pas-05-submit-mri-partial.json", pas_bundle("B-005", "PA-MRI-005", [("72148", CPT, "4", 2, None, "2026-10-05")]))
save("pas-06-submit-knee-needs-attachment.json", pas_bundle("B-006", "PA-KNEE-006", [("27447", CPT, "2", 1, "21", "2026-11-02")], extra=[knee_qr()], priority="stat"))
save("pas-07-submit-not-covered.json", pas_bundle("B-007", "PA-DERM-007", [("15780", CPT, "2", 1, None, "2026-10-10")]))
save("pas-08-submit-no-pa-needed.json", pas_bundle("B-008", "PA-OV-008", [("99213", CPT, "1", 1, None, "2026-10-10")]))
save("pas-09-submit-multi-line.json", pas_bundle("B-009", "PA-MULTI-009", [("72148", CPT, "4", 1, None, "2026-10-05"), ("99213", CPT, "1", 1, None, "2026-10-05"), ("15780", CPT, "2", 1, None, "2026-10-05")]))
save("pas-10-update-cancel-line.json", pas_bundle("B-010", "PA-MULTI-009", [("72148", CPT, "4", 1, None, "2026-10-05"), ("99213", CPT, "1", 1, None, "2026-10-05"), ("15780", CPT, "2", 1, None, "2026-10-05")], cancel_seq=1))
bad = pas_bundle("B-011", "PA-BAD-011", [("72148", CPT, "4", 1, None, "2026-10-05")])
bad["type"] = "transaction"; del bad["entry"][0]["resource"]["priority"]; bad["entry"][0]["resource"]["use"] = "claim"
save("pas-11-submit-invalid.json", bad)

inq = pas_bundle("B-INQ-1", "PA-KNEE-006", [("27447", CPT, "2", 1, "21", "2026-11-02")])
inq["meta"]["profile"] = [PAS + "profile-pas-inquiry-request-bundle"]
inq["entry"][0]["resource"]["meta"]["profile"] = [PAS + "profile-claim-inquiry"]
save("pas-12-inquire.json", inq)

save("pas-13-subscription.json", {
    "resourceType": "Subscription",
    "meta": {"profile": ["http://hl7.org/fhir/us/davinci-pas/StructureDefinition/profile-subscription"]},
    "status": "requested", "reason": "Pended prior authorization results for Good Health Clinic",
    "criteria": "http://hl7.org/fhir/us/davinci-pas/SubscriptionTopic/PASSubscriptionTopic",
    "_criteria": {"extension": [{"url": "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-filter-criteria",
                                 "valueString": f"ClaimResponse?orgIdentifier={NPI}|{ORG_NPI}"}]},
    "channel": {"type": "rest-hook", "endpoint": "https://ehr.example.org/pas-notifications",
                "payload": "application/fhir+json",
                "_payload": {"extension": [{"url": "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-payload-content",
                                            "valueCode": "id-only"}]},
                "header": ["Authorization: Bearer provider-endpoint-secret"]}})

# ------------------------------------------------------------------ CDex sample
imaging = {"resourceType": "DocumentReference", "id": "doc-xray",
           "status": "current",
           "type": {"coding": [{"system": "http://loinc.org", "code": "18748-4", "display": "Diagnostic imaging study"}]},
           "subject": {"reference": "Patient/pat-1"}, "date": "2026-09-20T09:00:00Z",
           "content": [{"attachment": {"contentType": "application/pdf", "title": "Knee X-ray report",
                                        "data": "JVBERi0xLjQKJcfsj6IKMSAwIG9iago8PC9UeXBlL0NhdGFsb2c+PgplbmRvYmoKdHJhaWxlcgo8PC9Sb290IDEgMCBSPj4KJSVFT0YK"}}]}
save("cdex-01-submit-attachment-knee.json", {
    "resourceType": "Parameters",
    "meta": {"profile": ["http://hl7.org/fhir/us/davinci-cdex/StructureDefinition/cdex-parameters-submit-attachment"]},
    "parameter": [
        {"name": "TrackingId", "valueIdentifier": {"system": "http://example.org/cdex/tracking-ids", "value": "{{Task.identifier[tracking-id].value}}"}},
        {"name": "AttachTo", "valueCode": "preauthorization"},
        {"name": "PayerId", "valueIdentifier": {"system": "http://example.org/payer-ids", "value": "PAYER001"}},
        {"name": "OrganizationId", "valueIdentifier": {"system": NPI, "value": ORG_NPI}},
        {"name": "ProviderId", "valueIdentifier": {"system": NPI, "value": ORDERING_NPI}},
        {"name": "MemberId", "valueIdentifier": {"system": "http://example.org/member-ids", "value": "MBR-1001"}},
        {"name": "Attachment", "part": [
            {"name": "LineItem", "valueString": "1"},
            {"name": "Code", "valueCodeableConcept": {"coding": [{"system": "http://loinc.org", "code": "18748-4"}]}},
            {"name": "Content", "resource": imaging}]}]})
save("cdex-02-submit-questionnaire-response.json", {
    "resourceType": "Parameters",
    "parameter": [
        {"name": "TrackingId", "valueIdentifier": {"system": "http://example.org/cdex/tracking-ids", "value": "{{Task.identifier[tracking-id].value}}"}},
        {"name": "AttachTo", "valueCode": "preauthorization"},
        {"name": "ProviderId", "valueIdentifier": {"system": NPI, "value": ORDERING_NPI}},
        {"name": "MemberId", "valueIdentifier": {"system": "http://example.org/member-ids", "value": "MBR-1001"}},
        {"name": "Attachment", "part": [
            {"name": "LineItem", "valueString": "1"},
            {"name": "Content", "resource": cpap_qr(22)}]}]})

print("samples written to", os.path.abspath(OUT))
