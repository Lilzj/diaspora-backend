namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// A member's stated position on one category.
/// <para>
/// <see cref="IsMuted"/> distinguishes the two, and they are not opposites: a mute is enforced
/// absolutely by a policy, a preference is a thumb on the scale in the interest strategy. See
/// <c>CategoryPreference</c>.
/// </para>
/// </summary>
public sealed record CategoryPreferenceResponse
{
    public required string CategoryKey { get; init; }

    /// <summary>Null means the preference applies wherever the category appears.</summary>
    public string? TargetType { get; init; }

    /// <summary>True: never show this. False: show more of this.</summary>
    public required bool IsMuted { get; init; }
}
