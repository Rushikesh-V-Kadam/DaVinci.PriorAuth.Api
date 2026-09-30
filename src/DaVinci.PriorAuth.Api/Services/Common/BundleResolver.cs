using DaVinci.PriorAuth.Models.Fhir;

namespace DaVinci.PriorAuth.Services.Common;

/// <summary>
/// Resolves references between entries of a collection Bundle (PAS request bundles use
/// fullUrl "urn:uuid:..." or "http://.../Patient/123", and references may be relative "Patient/123").
/// </summary>
public class BundleResolver
{
    private readonly Bundle _bundle;
    public BundleResolver(Bundle bundle) { _bundle = bundle; }

    public T? Resolve<T>(ResourceReference? reference) where T : Resource
    {
        if (reference?.Reference is not { Length: > 0 } r) return null;

        foreach (var entry in _bundle.Entry)
        {
            if (entry.Resource is not T typed) continue;
            if (entry.FullUrl == r) return typed;
            if (typed.Id != null && (r == $"{typed.ResourceType}/{typed.Id}" ||
                                     r.EndsWith($"/{typed.ResourceType}/{typed.Id}", StringComparison.Ordinal)))
                return typed;
            if (entry.FullUrl != null && entry.FullUrl.EndsWith("/" + r, StringComparison.Ordinal)) return typed;
        }
        return null;
    }

    public bool CanResolve(ResourceReference? reference) =>
        reference?.Identifier != null || Resolve<Resource>(reference) != null;

    public IEnumerable<T> All<T>() where T : Resource => _bundle.ResourcesOf<T>();
}
