namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>A member's position on one category.</summary>
public sealed record CategoryPreferenceRequest
{
    public required string CategoryKey { get; init; }

    /// <summary>Null applies the preference wherever the category appears.</summary>
    public string? TargetType { get; init; }

    /// <summary>True: never show this. False: show more of this.</summary>
    public required bool IsMuted { get; init; }

    /// <summary>
    /// Removes the preference entirely rather than setting it.
    /// <para>
    /// Distinct from setting <see cref="IsMuted"/> to false, which is a positive preference. Without
    /// this a member could unmute but never return to neutral.
    /// </para>
    /// </summary>
    public bool Clear { get; init; }
}
