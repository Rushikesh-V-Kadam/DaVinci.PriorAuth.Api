using System.Collections.Concurrent;
using DaVinci.PriorAuth.Models.Domain;
using DaVinci.PriorAuth.Models.Fhir;

namespace DaVinci.PriorAuth.Data;

// In-memory repositories so the API runs with no database. Each interface maps 1:1 to a
// table when you move to EF Core / SQL Server (see the guide, section "Moving to a database").

public interface IFhirResourceStore
{
    T Save<T>(T resource) where T : Resource;
    T? Get<T>(string id) where T : Resource;
    IEnumerable<T> All<T>() where T : Resource;
}

public class InMemoryFhirResourceStore : IFhirResourceStore
{
    private readonly ConcurrentDictionary<string, Resource> _items = new();

    public T Save<T>(T resource) where T : Resource
    {
        resource.Id ??= Guid.NewGuid().ToString("N");
        resource.Meta ??= new Meta();
        resource.Meta.LastUpdated = FhirDate.Now();
        resource.Meta.VersionId = ((int.TryParse(resource.Meta.VersionId, out var v) ? v : 0) + 1).ToString();
        _items[$"{resource.ResourceType}/{resource.Id}"] = resource;
        return resource;
    }

    public T? Get<T>(string id) where T : Resource =>
        _items.Values.OfType<T>().FirstOrDefault(r => r.Id == id);

    public IEnumerable<T> All<T>() where T : Resource => _items.Values.OfType<T>();
}

public interface ICoverageRuleRepository
{
    CoverageRule? Find(string? planId, IEnumerable<Coding> codes);
    IEnumerable<CoverageRule> All();
    void Add(CoverageRule rule);
}

public class InMemoryCoverageRuleRepository : ICoverageRuleRepository
{
    private readonly List<CoverageRule> _rules = new();
    private readonly object _lock = new();

    public void Add(CoverageRule rule) { lock (_lock) _rules.Add(rule); }

    public IEnumerable<CoverageRule> All() { lock (_lock) return _rules.ToList(); }

    /// <summary>Plan-specific rule wins over the "*" (all plans) rule. Code system must match (HCPCS OID accepted).</summary>
    public CoverageRule? Find(string? planId, IEnumerable<Coding> codes)
    {
        var codeList = codes.ToList();
        lock (_lock)
        {
            var matches = _rules.Where(r => codeList.Any(c => c.Code == r.Code && SameSystem(c.System, r.CodeSystem))).ToList();
            return matches.FirstOrDefault(r => r.PlanId == planId) ?? matches.FirstOrDefault(r => r.PlanId == "*");
        }
    }

    private static bool SameSystem(string? a, string b)
    {
        if (a == b) return true;
        var hcpcs = new[] { Constants.CodeSystems.Hcpcs, Constants.CodeSystems.HcpcsOid };
        return a != null && hcpcs.Contains(a) && hcpcs.Contains(b);
    }
}

public interface ICoverageAssertionRepository
{
    void Save(CoverageAssertion assertion);
    CoverageAssertion? Get(string coverageAssertionId);
}

public class InMemoryCoverageAssertionRepository : ICoverageAssertionRepository
{
    private readonly ConcurrentDictionary<string, CoverageAssertion> _items = new();
    public void Save(CoverageAssertion assertion) => _items[assertion.CoverageAssertionId] = assertion;
    public CoverageAssertion? Get(string id) => _items.TryGetValue(id, out var a) ? a : null;
}

public interface IPriorAuthRepository
{
    void Save(PriorAuthRecord record);
    PriorAuthRecord? Get(string id);
    PriorAuthRecord? FindByBundleIdentifier(string key);
    PriorAuthRecord? FindActiveByClaimIdentifier(string key);
    PriorAuthRecord? FindByTrackingId(string trackingId);
    PriorAuthRecord? FindByPreAuthRef(string preAuthRef);
    IEnumerable<PriorAuthRecord> FindByMember(string memberId);
    IEnumerable<PriorAuthRecord> Pending();
}

public class InMemoryPriorAuthRepository : IPriorAuthRepository
{
    private readonly ConcurrentDictionary<string, PriorAuthRecord> _items = new();

    public void Save(PriorAuthRecord record) => _items[record.Id] = record;
    public PriorAuthRecord? Get(string id) => _items.TryGetValue(id, out var r) ? r : null;

    public PriorAuthRecord? FindByBundleIdentifier(string key) =>
        _items.Values.FirstOrDefault(r => r.BundleIdentifierKey == key);

    public PriorAuthRecord? FindActiveByClaimIdentifier(string key) =>
        _items.Values.Where(r => r.ClaimIdentifierKey == key && r.Status != PriorAuthStatus.Replaced)
                     .OrderByDescending(r => r.Version).FirstOrDefault();

    public PriorAuthRecord? FindByTrackingId(string trackingId) =>
        _items.Values.FirstOrDefault(r => r.TrackingId == trackingId && r.Status != PriorAuthStatus.Replaced);

    public PriorAuthRecord? FindByPreAuthRef(string preAuthRef) =>
        _items.Values.FirstOrDefault(r => r.PreAuthRef == preAuthRef);

    public IEnumerable<PriorAuthRecord> FindByMember(string memberId) =>
        _items.Values.Where(r => r.PatientMemberId == memberId);

    public IEnumerable<PriorAuthRecord> Pending() =>
        _items.Values.Where(r => r.Status == PriorAuthStatus.Pended);
}

public interface ISubscriptionRepository
{
    void Save(SubscriptionRecord record);
    SubscriptionRecord? Get(string id);
    IEnumerable<SubscriptionRecord> ForOrganization(string orgIdentifierKey);
}

public class InMemorySubscriptionRepository : ISubscriptionRepository
{
    private readonly ConcurrentDictionary<string, SubscriptionRecord> _items = new();
    public void Save(SubscriptionRecord record) => _items[record.Id] = record;
    public SubscriptionRecord? Get(string id) => _items.TryGetValue(id, out var r) ? r : null;

    public IEnumerable<SubscriptionRecord> ForOrganization(string orgIdentifierKey) =>
        _items.Values.Where(s => s.Resource.Status == "active" &&
            (s.OrgIdentifierFilter == orgIdentifierKey || s.OrgIdentifierFilter.EndsWith("|" + orgIdentifierKey.Split('|').Last())));
}
