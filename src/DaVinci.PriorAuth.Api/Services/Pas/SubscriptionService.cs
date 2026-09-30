using DaVinci.PriorAuth.Constants;
using DaVinci.PriorAuth.Data;
using DaVinci.PriorAuth.Infrastructure;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;
using DaVinci.PriorAuth.Services.Common;
using Microsoft.Extensions.Options;

namespace DaVinci.PriorAuth.Services.Pas;

public interface ISubscriptionService
{
    (Subscription? Created, OperationOutcome? Error) Create(Subscription subscription);
    Subscription? Get(string id);
    Task NotifyAsync(PriorAuthRecord record, CancellationToken ct);
}

/// <summary>
/// PAS pended-result notifications (R4 Subscriptions R5 Backport, rest-hook).
/// The provider subscribes per requesting organization:
///   criteria  = PAS SubscriptionTopic canonical
///   _criteria.extension[backport-filter-criteria] = "ClaimResponse?orgIdentifier=system|value"
///   channel.type = rest-hook, channel.endpoint, channel.payload + backport-payload-content (id-only|full-resource)
/// When a pended request gets a decision, a notification Bundle (type history) is POSTed to the endpoint.
/// The provider then calls Claim/$inquire (id-only) or reads the ClaimResponse (full-resource).
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repo;
    private readonly EhrFhirClient _client;
    private readonly PayerOptions _options;
    private readonly ILogger<SubscriptionService> _log;

    public SubscriptionService(ISubscriptionRepository repo, EhrFhirClient client, IOptions<PayerOptions> options, ILogger<SubscriptionService> log)
    {
        _repo = repo; _client = client; _options = options.Value; _log = log;
    }

    public (Subscription? Created, OperationOutcome? Error) Create(Subscription s)
    {
        var oo = new OperationOutcome();
        if (s.Criteria != PasSubscription.TopicCanonical)
            oo.Add(IssueSeverity.Error, IssueType.NotSupported, $"Subscription.criteria must be {PasSubscription.TopicCanonical}", "Subscription.criteria");

        var filter = s.CriteriaExtensions?.Extension.GetByUrl(PasSubscription.BackportFilterCriteria)?.ValueString;
        var orgFilter = ParseOrgFilter(filter);
        if (orgFilter == null)
            oo.Add(IssueSeverity.Error, IssueType.Required,
                $"A backport-filter-criteria extension 'ClaimResponse?{PasSubscription.FilterParameter}=system|value' is required.",
                "Subscription._criteria.extension");

        if (s.Channel.Type != "rest-hook") oo.Add(IssueSeverity.Error, IssueType.NotSupported, "Only rest-hook channels are supported.", "Subscription.channel.type");
        if (string.IsNullOrEmpty(s.Channel.Endpoint)) oo.Add(IssueSeverity.Error, IssueType.Required, "Subscription.channel.endpoint is required.", "Subscription.channel.endpoint");
        if (oo.HasErrors) return (null, oo);

        var content = s.Channel.PayloadExtensions?.Extension.GetByUrl(PasSubscription.BackportPayloadContent)?.ValueCode ?? PasSubscription.PayloadIdOnly;
        s.Id = Guid.NewGuid().ToString("N");
        s.Status = "active";
        s.Meta = new Meta { LastUpdated = FhirDate.Now() };

        _repo.Save(new SubscriptionRecord
        {
            Id = s.Id,
            Resource = s,
            OrgIdentifierFilter = orgFilter!,
            Endpoint = s.Channel.Endpoint!,
            PayloadContent = content,
            Headers = s.Channel.Header
        });

        // handshake so the client knows the endpoint works (fire and forget)
        _ = SendAsync(_repo.Get(s.Id)!, "handshake", null, CancellationToken.None);
        return (s, null);
    }

    public Subscription? Get(string id) => _repo.Get(id)?.Resource;

    public async Task NotifyAsync(PriorAuthRecord record, CancellationToken ct)
    {
        if (record.RequestorOrgIdentifierKey == null || record.LatestResponseBundle == null) return;
        var claimResponse = record.LatestResponseBundle.ResourcesOf<ClaimResponse>().FirstOrDefault();
        foreach (var sub in _repo.ForOrganization(record.RequestorOrgIdentifierKey))
        {
            sub.EventCount++;
            _repo.Save(sub);
            await SendAsync(sub, "event-notification", claimResponse, ct);
        }
    }

    private async Task SendAsync(SubscriptionRecord sub, string type, ClaimResponse? focus, CancellationToken ct)
    {
        var status = new Parameters();
        status.WithProfile(PasSubscription.BackportStatusProfile);
        status.Parameter.Add(new ParametersParameter { Name = "subscription", ValueReference = new ResourceReference($"{_options.BaseUrl}/fhir/Subscription/{sub.Id}") });
        status.Parameter.Add(new ParametersParameter { Name = "topic", ValueCanonical = PasSubscription.TopicCanonical });
        status.Parameter.Add(new ParametersParameter { Name = "status", ValueCode = "active" });
        status.Parameter.Add(new ParametersParameter { Name = "type", ValueCode = type });
        status.Parameter.Add(new ParametersParameter { Name = "events-since-subscription-start", ValueString = sub.EventCount.ToString() });

        var notification = new Bundle { Id = Guid.NewGuid().ToString("N"), Type = "history", Timestamp = FhirDate.Now() };
        notification.WithProfile(PasSubscription.BackportNotificationProfile);
        notification.Entry.Add(new BundleEntry { FullUrl = $"urn:uuid:{Guid.NewGuid():N}", Resource = status });

        if (focus != null)
        {
            var focusUrl = $"{_options.BaseUrl}/fhir/ClaimResponse/{focus.Id}";
            status.Parameter.Add(new ParametersParameter
            {
                Name = "notification-event",
                Part =
                {
                    new ParametersParameter { Name = "event-number", ValueString = sub.EventCount.ToString() },
                    new ParametersParameter { Name = "timestamp", ValueInstant = FhirDate.Now() },
                    new ParametersParameter { Name = "focus", ValueReference = new ResourceReference(focusUrl) }
                }
            });
            if (sub.PayloadContent == PasSubscription.PayloadFullResource)
                notification.Entry.Add(new BundleEntry { FullUrl = focusUrl, Resource = focus });
        }

        var ok = await _client.PostAsync(sub.Endpoint, notification, ct, sub.Headers);
        _log.LogInformation("Subscription {Id} {Type} -> {Endpoint}: {Result}", sub.Id, type, sub.Endpoint, ok ? "delivered" : "failed");
    }

    /// <summary>"ClaimResponse?orgIdentifier=http://hl7.org/fhir/sid/us-npi|1234567893" -> "system|value"</summary>
    private static string? ParseOrgFilter(string? criteria)
    {
        if (string.IsNullOrEmpty(criteria)) return null;
        var idx = criteria.IndexOf(PasSubscription.FilterParameter + "=", StringComparison.Ordinal);
        if (idx < 0) return null;
        var value = criteria[(idx + PasSubscription.FilterParameter.Length + 1)..].Split('&')[0];
        return Uri.UnescapeDataString(value);
    }
}
