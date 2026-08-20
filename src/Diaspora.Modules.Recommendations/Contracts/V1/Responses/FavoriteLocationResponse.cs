namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// A place the member asked to see more of.
/// <para>
/// Stated rather than derived, which is the whole point: somebody planning a move has no signals in
/// the country they are moving to, and an affinity cannot represent an intention.
/// </para>
/// </summary>
public sealed record FavoriteLocationResponse
{
    /// <summary><c>Country</c>, <c>City</c> or <c>Neighborhood</c>.</summary>
    public required string Scope { get; init; }

    public required string Key { get; init; }

    public string? DisplayName { get; init; }
}
