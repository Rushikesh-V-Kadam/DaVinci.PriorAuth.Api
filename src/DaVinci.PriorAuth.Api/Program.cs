using System.Text.Json;
using System.Text.Json.Serialization;
using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Cdex;
using DaVinci.PriorAuth.Services.Common;
using DaVinci.PriorAuth.Services.Crd;
using DaVinci.PriorAuth.Services.Dtr;
using DaVinci.PriorAuth.Services.Pas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuration
builder.Services.Configure<PayerOptions>(builder.Configuration.GetSection(PayerOptions.Section));

// ---------------------------------------------------------------- MVC (CDS Hooks JSON uses these options)
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Register Swagger generator
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.OperationFilter<SwaggerFhirBodyFilter>());

// DTR SMART apps run in the browser and call $questionnaire-package directly
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---------------------------------------------------------------- security
// Production: SMART Backend Services (client_credentials + private_key_jwt) access tokens.
//   1) dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.*
//   2) uncomment the block below and set "Auth:Authority" / "Auth:Audience" in appsettings.json
//   3) set "Payer:RequireAuthentication": true
//
// builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(o =>
//     {
//         o.Authority = builder.Configuration["Auth:Authority"];
//         o.Audience = builder.Configuration["Auth:Audience"];
//     });
builder.Services.AddAuthentication();   // schemes are added above in production
builder.Services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
builder.Services.AddAuthorization(o =>
{
    // CDS Hooks: the EHR signs a JWT per call (iss = EHR, validated against the EHR's JWKS) - no SMART scope
    o.AddPolicy(Policies.CrdHooks, p => p.AddRequirements(new ScopeRequirement()));
    o.AddPolicy(Policies.DtrRead, p => p.AddRequirements(new ScopeRequirement(Scopes.QuestionnaireRead, Scopes.QuestionnaireReadSystem)));
    o.AddPolicy(Policies.PasSubmit, p => p.AddRequirements(new ScopeRequirement(Scopes.ClaimCreate, Scopes.ClaimWrite)));
    o.AddPolicy(Policies.CdexSubmit, p => p.AddRequirements(new ScopeRequirement(Scopes.AttachmentSubmit, Scopes.TaskRead)));
});

// ---------------------------------------------------------------- data (in-memory; swap for EF Core later)
builder.Services.AddSingleton<IFhirResourceStore, InMemoryFhirResourceStore>();
builder.Services.AddSingleton<ICoverageRuleRepository, InMemoryCoverageRuleRepository>();
builder.Services.AddSingleton<ICoverageAssertionRepository, InMemoryCoverageAssertionRepository>();
builder.Services.AddSingleton<IPriorAuthRepository, InMemoryPriorAuthRepository>();
builder.Services.AddSingleton<ISubscriptionRepository, InMemorySubscriptionRepository>();

// ---------------------------------------------------------------- services
builder.Services.AddHttpClient<EhrFhirClient>(c => c.Timeout = TimeSpan.FromSeconds(4)); // CRD must answer within ~5s

// CRD
builder.Services.AddScoped<OrderExtractor>();
builder.Services.AddScoped<ICoverageRulesEngine, CoverageRulesEngine>();
builder.Services.AddScoped<ICrdService, CrdService>();
// DTR
builder.Services.AddScoped<IQuestionnairePackageService, QuestionnairePackageService>();
builder.Services.AddSingleton<IAdaptiveQuestionnaireService, AdaptiveQuestionnaireService>();
// PAS
builder.Services.AddScoped<IPasAdjudicator, PasAdjudicator>();
builder.Services.AddScoped<IPasResponseBuilder, PasResponseBuilder>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPasService, PasService>();
// CDex
builder.Services.AddScoped<IAdditionalInfoRequestBuilder, AdditionalInfoRequestBuilder>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

var app = builder.Build();

// ---------------------------------------------------------------- seed policy table, questionnaires, CQL
SeedData.Load(app.Services, app.Environment.ContentRootPath);

// Unhandled errors -> OperationOutcome (FHIR clients expect one)
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = 500;
    context.Response.ContentType = MediaTypes.FhirJson;
    var oo = OperationOutcome.Create(IssueSeverity.Fatal, IssueType.Exception, error?.Message ?? "Unexpected error");
    await context.Response.WriteAsync(FhirJson.Serialize(oo));
}));

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed for WebApplicationFactory integration tests.</summary>
public partial class Program { }
