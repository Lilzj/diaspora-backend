namespace Diaspora.Contracts.CommunityProgramme.V1.Directory;

/// <summary>
/// Communities a member could join, read by Recommendations for <c>GET /recommendations/communities</c>.
/// <para>
/// <b>Why a seam.</b> The community aggregate, its presence countries and the member's standing in
/// each belong to CommunityProgramme, which is composed after Recommendations; Recommendations ranks
/// and explains, and so it asks here rather than referencing the module. No community event was ever
/// projected into the recommendation candidates, which is why the surface answered nothing.
/// </para>
/// <para>
/// <b>The directory's own rules apply</b>: public and active only, and never a community the member
/// already belongs to, is waiting on, was declined by or was removed from.
/// </para>
/// </summary>
public interface ICommunitySuggestionSource
{
    Task<IReadOnlyList<CommunitySuggestionCandidate>> ListAsync(
        CommunitySuggestionQuery query, CancellationToken cancellationToken = default);
}

public sealed record CommunitySuggestionQuery
{
    public required Guid ViewerUserId { get; init; }

    /// <summary>
    /// ISO 3166-1 alpha-2. When set, only communities that declare a presence there are returned —
    /// the same predicate as the directory's <c>presence</c> filter.
    /// </summary>
    public string? PresenceCountryCode { get; init; }

    /// <summary>How many to read. The implementation caps it.</summary>
    public int Take { get; init; } = 50;
}

/// <summary>One joinable community, with the facts a ranking may cite.</summary>
public sealed record CommunitySuggestionCandidate
{
    public required Guid CommunityId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public string? Tagline { get; init; }

    /// <summary><c>CommunityType</c> name.</summary>
    public required string Type { get; init; }

    /// <summary>Where its people are from.</summary>
    public string? OriginCountryCode { get; init; }

    public string? OriginRegionName { get; init; }

    /// <summary>Where it declares a presence.</summary>
    public required IReadOnlyList<string> PresenceCountryCodes { get; init; }

    public string? PrimaryLanguage { get; init; }

    public IReadOnlyList<string> SupportedLanguages { get; init; } = [];

    public Guid? LogoMediaId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
