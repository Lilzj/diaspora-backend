using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// A member's stated position on one category.
/// <para>
/// <b>Muting and preferring are not opposites, and this is why they share one row rather than two
/// tables.</b> A mute is enforced by a policy and is absolute — the category never appears. A
/// preference is consumed by the interest strategy and is a thumb on the scale. Modelling them as
/// one field with two states makes it impossible to hold both for one category, which is a state
/// nobody could act on sensibly.
/// </para>
/// </summary>
public sealed class CategoryPreference : ValueObject
{
    private CategoryPreference(
        Guid preferenceId,
        RecommendationTargetType? targetType,
        string categoryKey,
        bool isMuted)
    {
        PreferenceId = preferenceId;
        TargetType = targetType;
        CategoryKey = categoryKey;
        IsMuted = isMuted;
    }

    private CategoryPreference() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid PreferenceId { get; private set; }

    /// <summary>Null means the category is muted or preferred wherever it appears.</summary>
    public RecommendationTargetType? TargetType { get; private set; }

    public string CategoryKey { get; private set; } = string.Empty;

    /// <summary>True: never show this. False: show more of this.</summary>
    public bool IsMuted { get; private set; }

    public static CategoryPreference Create(
        Guid preferenceId,
        string categoryKey,
        bool isMuted,
        RecommendationTargetType? targetType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        return new CategoryPreference(
            preferenceId, targetType, categoryKey.Trim().ToLowerInvariant(), isMuted);
    }

    public void SetMuted(bool isMuted) => IsMuted = isMuted;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PreferenceId;
        yield return TargetType;
        yield return CategoryKey;
        yield return IsMuted;
    }
}
