using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// One thing a member has shown interest in, and how strongly.
/// <para>
/// <b>Affinity is a bounded running average, not a running total, and the difference decides whether
/// the profile is any use after a year.</b> A total grows without limit, so a member who read forty
/// housing guides during one stressful fortnight would be permanently classified as house-hunting;
/// a bounded value saturates, which means recency can move it back down.
/// </para>
/// <para>
/// <see cref="ObservationCount"/> is kept alongside because confidence needs it. An affinity of 0.8
/// from two observations and an affinity of 0.8 from ninety are the same number and not the same
/// claim, and the explanation the member sees says so.
/// </para>
/// </summary>
public sealed class ProfileInterest : ValueObject
{
    /// <summary>How much one fresh observation can move an affinity. Deliberately small.</summary>
    private const decimal LearningRate = 0.15m;

    private ProfileInterest(Guid interestId, InterestKind kind, string key, bool isExplicit)
    {
        InterestId = interestId;
        Kind = kind;
        Key = key;
        IsExplicit = isExplicit;
    }

    private ProfileInterest() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid InterestId { get; private set; }

    public InterestKind Kind { get; private set; }

    /// <summary>The category, tag, skill, language or term. Normalised lower case.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Between 0 and 1. See the class summary for why it is bounded.</summary>
    public decimal Affinity { get; private set; }

    public int ObservationCount { get; private set; }

    public DateTimeOffset? LastObservedAt { get; private set; }

    /// <summary>
    /// Whether the member said this rather than the platform inferring it.
    /// <para>
    /// Stated interests are never decayed and never pruned. A member who ticked "immigration" during
    /// onboarding and then read nothing for six months still meant it.
    /// </para>
    /// </summary>
    public bool IsExplicit { get; private set; }

    public static ProfileInterest Create(
        Guid interestId, InterestKind kind, string key, bool isExplicit = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new ProfileInterest(interestId, kind, key.Trim().ToLowerInvariant(), isExplicit);
    }

    /// <summary>
    /// Folds one observation in.
    /// <para>
    /// <paramref name="strength"/> is the signal's weight — a view moves this far less than an
    /// application does — and the exponential form means the same observation repeated has
    /// diminishing effect, which is the behaviour a bounded affinity needs.
    /// </para>
    /// </summary>
    public void Observe(decimal strength, DateTimeOffset observedAt)
    {
        var clamped = Math.Clamp(strength, 0m, 1m);

        Affinity = decimal.Round(
            Affinity + ((clamped - Affinity) * LearningRate), 6);

        Affinity = Math.Clamp(Affinity, 0m, 1m);
        ObservationCount++;

        if (LastObservedAt is null || observedAt > LastObservedAt)
        {
            LastObservedAt = observedAt;
        }
    }

    /// <summary>
    /// Ages the affinity towards zero.
    /// <para>
    /// Applied by the profile builder rather than by a timer, so a profile that is never rebuilt is
    /// stale rather than silently wrong — which is the honest failure of the two.
    /// </para>
    /// </summary>
    public void Decay(decimal factor)
    {
        if (IsExplicit)
        {
            return;
        }

        Affinity = decimal.Round(Math.Clamp(Affinity * Math.Clamp(factor, 0m, 1m), 0m, 1m), 6);
    }

    /// <summary>Sets the affinity outright. The rebuild path, which recomputes from scratch.</summary>
    public void SetAffinity(decimal value, int observationCount, DateTimeOffset? lastObservedAt)
    {
        Affinity = decimal.Round(Math.Clamp(value, 0m, 1m), 6);
        ObservationCount = Math.Max(0, observationCount);
        LastObservedAt = lastObservedAt;
    }

    public void MarkExplicit() => IsExplicit = true;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return InterestId;
        yield return Kind;
        yield return Key;
        yield return Affinity;
        yield return ObservationCount;
        yield return IsExplicit;
    }
}
