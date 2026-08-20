using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// How attached a member is to one place, at one grain.
/// <para>
/// <b>Several places at once is the normal case, and that is the whole reason this is a collection
/// rather than three columns on the profile.</b> The platform's members are, by definition, people
/// with more than one country: somebody in Port Louis who reads Lagos guides every week, an intended
/// mover researching a city they have never seen, a student home for the summer. A profile that held
/// one country would be wrong for most of them.
/// </para>
/// <para>
/// <see cref="IsPrimary"/> marks the one place the member actually is, as Identity or Location
/// reported it. It is the anchor cold start uses, and it is set rather than inferred — deriving
/// "where do they live" from "what do they read" is how a platform tells somebody researching a move
/// that they already live there.
/// </para>
/// </summary>
public sealed class ProfileLocationAffinity : ValueObject
{
    private const decimal LearningRate = 0.2m;

    private ProfileLocationAffinity(
        Guid affinityId, AffinityScope scope, string key, string? displayName)
    {
        AffinityId = affinityId;
        Scope = scope;
        Key = key;
        DisplayName = displayName;
    }

    private ProfileLocationAffinity() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid AffinityId { get; private set; }

    public AffinityScope Scope { get; private set; }

    /// <summary>An ISO country code, or a normalised city or neighbourhood name.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>What to call it on screen. The reason builder needs it and will not look it up.</summary>
    public string? DisplayName { get; private set; }

    public decimal Affinity { get; private set; }

    public int ObservationCount { get; private set; }

    public DateTimeOffset? LastObservedAt { get; private set; }

    /// <summary>Where the member actually is, as reported. Never inferred — see the class summary.</summary>
    public bool IsPrimary { get; private set; }

    public static ProfileLocationAffinity Create(
        Guid affinityId, AffinityScope scope, string key, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new ProfileLocationAffinity(
            affinityId,
            scope,
            scope == AffinityScope.Country ? key.Trim().ToUpperInvariant() : key.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim());
    }

    public void Observe(decimal strength, DateTimeOffset observedAt)
    {
        var clamped = Math.Clamp(strength, 0m, 1m);

        Affinity = decimal.Round(
            Math.Clamp(Affinity + ((clamped - Affinity) * LearningRate), 0m, 1m), 6);

        ObservationCount++;

        if (LastObservedAt is null || observedAt > LastObservedAt)
        {
            LastObservedAt = observedAt;
        }
    }

    public void Decay(decimal factor)
    {
        if (IsPrimary)
        {
            return;
        }

        Affinity = decimal.Round(Math.Clamp(Affinity * Math.Clamp(factor, 0m, 1m), 0m, 1m), 6);
    }

    /// <summary>
    /// Declares this the member's actual location.
    /// <para>
    /// Sets the affinity to its ceiling as well: wherever a member is, that is the place the
    /// location strategy should score highest, regardless of how much reading they have done
    /// elsewhere.
    /// </para>
    /// </summary>
    public void MarkPrimary(string? displayName, DateTimeOffset observedAt)
    {
        IsPrimary = true;
        Affinity = 1m;
        LastObservedAt = observedAt;

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }
    }

    public void ClearPrimary() => IsPrimary = false;

    public void SetAffinity(decimal value, int observationCount, DateTimeOffset? lastObservedAt)
    {
        Affinity = decimal.Round(Math.Clamp(value, 0m, 1m), 6);
        ObservationCount = Math.Max(0, observationCount);
        LastObservedAt = lastObservedAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AffinityId;
        yield return Scope;
        yield return Key;
        yield return Affinity;
        yield return ObservationCount;
        yield return IsPrimary;
    }
}
