using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// Whether one surface is personalised for this member.
/// <para>
/// <b>Per-surface rather than all-or-nothing, because the two objections people have to
/// personalization are different objections.</b> "Do not use my behaviour to pick my jobs" is a
/// privacy position; "the home feed keeps showing me housing and I have already moved" is a product
/// complaint. One switch would force the second person to take the first person's remedy.
/// </para>
/// <para>
/// Absence means enabled. A member who has never opened the settings screen gets a personalised
/// feed, which is the behaviour the product ships with — the opt-out is recorded, never the opt-in,
/// so a row here always means somebody made a choice.
/// </para>
/// </summary>
public sealed class SurfacePreference : ValueObject
{
    private SurfacePreference(Guid preferenceId, RecommendationSurface surface, bool isEnabled)
    {
        PreferenceId = preferenceId;
        Surface = surface;
        IsEnabled = isEnabled;
    }

    private SurfacePreference() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid PreferenceId { get; private set; }

    public RecommendationSurface Surface { get; private set; }

    public bool IsEnabled { get; private set; }

    public static SurfacePreference Create(
        Guid preferenceId, RecommendationSurface surface, bool isEnabled) =>
        new(preferenceId, surface, isEnabled);

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PreferenceId;
        yield return Surface;
        yield return IsEnabled;
    }
}
