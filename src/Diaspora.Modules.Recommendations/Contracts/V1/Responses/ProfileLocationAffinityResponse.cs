namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// How attached a member is to one place.
/// <para>
/// <see cref="IsPrimary"/> marks where they actually are, as Identity or Trust reported it; the rest
/// are places they keep looking at. Several at once is the normal case for this platform's members —
/// see <c>ProfileLocationAffinity</c>.
/// </para>
/// </summary>
public sealed record ProfileLocationAffinityResponse
{
    /// <summary><c>Country</c>, <c>City</c> or <c>Neighborhood</c>.</summary>
    public required string Scope { get; init; }

    public required string Key { get; init; }

    public string? DisplayName { get; init; }

    public required decimal Affinity { get; init; }

    /// <summary>Where the member actually is. Never inferred from what they read.</summary>
    public required bool IsPrimary { get; init; }
}
