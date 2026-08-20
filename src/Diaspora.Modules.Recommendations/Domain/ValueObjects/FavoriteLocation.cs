using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// A place the member asked to see more of.
/// <para>
/// <b>Stated, and therefore treated differently from a derived affinity.</b> Somebody planning a
/// move to Cape Town has no signals there at all — no views, no saves, nothing to derive from — and
/// the whole reason this exists is that the platform's members routinely care about a place before
/// they have any history in it. A derived affinity cannot represent an intention.
/// </para>
/// <para>
/// Fed into the profile as an explicit location affinity on every rebuild, which is what stops a
/// rebuild from quietly discarding it.
/// </para>
/// </summary>
public sealed class FavoriteLocation : ValueObject
{
    private FavoriteLocation(Guid locationId, AffinityScope scope, string key, string? displayName)
    {
        LocationId = locationId;
        Scope = scope;
        Key = key;
        DisplayName = displayName;
    }

    private FavoriteLocation() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid LocationId { get; private set; }

    public AffinityScope Scope { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string? DisplayName { get; private set; }

    public static FavoriteLocation Create(
        Guid locationId, AffinityScope scope, string key, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new FavoriteLocation(
            locationId,
            scope,
            scope == AffinityScope.Country ? key.Trim().ToUpperInvariant() : key.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LocationId;
        yield return Scope;
        yield return Key;
    }
}
