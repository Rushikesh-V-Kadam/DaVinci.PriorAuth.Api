# Da Vinci Prior Authorization API (.NET 8)

Payer-side implementation of the HL7 Da Vinci Burden Reduction guides named in CMS-0057-F:
CRD 2.0.1, DTR 2.0.1, PAS 2.0.1, CDex 2.0.0 (FHIR R4 4.0.1).

No NuGet packages are needed to build and run (System.Text.Json + ASP.NET Core only).

```
dotnet build src/DaVinci.PriorAuth.Api
dotnet run --project src/DaVinci.PriorAuth.Api        # http://localhost:5080
./tools/run-scenarios.sh                              # needs bash, curl, jq
```

| Area | Endpoint |
|------|----------|
| CRD  | GET /cds-services, POST /cds-services/crd-{hook} |
| DTR  | POST /fhir/Questionnaire/$questionnaire-package, POST /fhir/Questionnaire/$next-question |
| PAS  | POST /fhir/Claim/$submit, POST /fhir/Claim/$inquire, POST /fhir/Subscription |
| CDex | POST /fhir/$submit-attachment, GET/PUT /fhir/Task |
| UM   | GET /um/prior-auth, POST /um/prior-auth/{id}/items/{seq}/decision (payer-internal) |

See `DaVinci_PriorAuth_Implementation_Guide.pdf` for the step-by-step guide.
Regenerate the sample requests with `python3 tools/make_samples.py`.
