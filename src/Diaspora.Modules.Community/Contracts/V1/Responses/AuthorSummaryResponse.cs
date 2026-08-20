namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// Who wrote something, as a card renders them.
/// <para>
/// Field names taken from the client's own author summary. Everything here is either B6's or
/// resolved from Trust and Location at projection time — B6 stores no member profile of its own.
/// </para>
/// </summary>
public sealed record AuthorSummaryResponse
{
    public required Guid UserId { get; init; }

    public string? Name { get; init; }

    /// <summary>A Media reference, not a URL. The client resolves it through Media.</summary>
    public Guid? AvatarMediaId { get; init; }

    /// <summary>From Trust. Never computed here.</summary>
    public string? ReputationLevel { get; init; }

    public bool IsCountryVerified { get; init; }

    public string? PrimaryCountryCode { get; init; }

    public int FollowerCount { get; init; }

    public bool IsFollowedByUser { get; init; }

    /// <summary>Set when the post was published as an organization.</summary>
    public Guid? OrganizationId { get; init; }

    public string? OrganizationName { get; init; }

    public string? PublishingIdentityType { get; init; }
}
