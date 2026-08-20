namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>A place the caller wants to see more of.</summary>
public sealed record FavoriteLocationRequest
{
    /// <summary><c>Country</c>, <c>City</c> or <c>Neighborhood</c>.</summary>
    public required string Scope { get; init; }

    public required string Key { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>Removes the favourite rather than adding it.</summary>
    public bool Remove { get; init; }
}
