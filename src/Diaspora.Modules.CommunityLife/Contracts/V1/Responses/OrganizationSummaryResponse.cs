namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>An organization as a card renders it. What every list screen carries.</summary>
public sealed record OrganizationSummaryResponse
{
    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    public string? ShortName { get; init; }

    public required string Slug { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public required string Summary { get; init; }

    public required LocationResponse Location { get; init; }

    public Guid? LogoMediaId { get; init; }

    public Guid? CoverMediaId { get; init; }

    public required bool IsVerified { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool AcceptsMembership { get; init; }

    public required int MemberCount { get; init; }

    public required int PublishedEventCount { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    public DateTimeOffset? LastActivityAt { get; init; }
}
