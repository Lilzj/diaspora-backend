namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// An organization, in full.
/// <para>
/// What a detail screen renders. The list screens use <see cref="OrganizationSummaryResponse"/>,
/// which is deliberately much smaller — a directory page carrying every leader and every opening
/// hour would be several hundred kilobytes.
/// </para>
/// </summary>
public sealed record OrganizationResponse
{
    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    public string? ShortName { get; init; }

    public required string Slug { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public string? OrganizationTypeName { get; init; }

    public required string Summary { get; init; }

    public string? Description { get; init; }

    public string? Mission { get; init; }

    public string? Vision { get; init; }

    public required LocationResponse Location { get; init; }

    public required ContactResponse Contact { get; init; }

    public required SocialLinksResponse SocialLinks { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    public IReadOnlyList<string> Services { get; init; } = [];

    public IReadOnlyList<OrganizationLeaderResponse> Leaders { get; init; } = [];

    public IReadOnlyList<OperatingHourResponse> OperatingHours { get; init; } = [];

    public IReadOnlyList<MediaReferenceResponse> Media { get; init; } = [];

    public IReadOnlyList<GuideReferenceResponse> Guides { get; init; } = [];

    public Guid? LogoMediaId { get; init; }

    public Guid? CoverMediaId { get; init; }

    public required string Status { get; init; }

    public required string Visibility { get; init; }

    public required string ModerationState { get; init; }

    public required string MembershipPolicy { get; init; }

    public string? MembershipRequirements { get; init; }

    public required bool IsMembershipPublic { get; init; }

    public required bool AcceptsMembership { get; init; }

    public required bool IsVerified { get; init; }

    public string? VerificationBadge { get; init; }

    public required string VerificationState { get; init; }

    public required bool IsFeatured { get; init; }

    public required MembershipStatisticsResponse Statistics { get; init; }

    /// <summary>The B7 provider behind it, when there is one.</summary>
    public Guid? ProviderId { get; init; }

    /// <summary>The caller's own standing, when they have one. Null for a stranger.</summary>
    public string? ViewerMembershipStatus { get; init; }

    public string? ViewerMembershipRole { get; init; }

    /// <summary>How complete the page is, nought to one. A prompt, not a quality score.</summary>
    public required double Completeness { get; init; }

    /// <summary>The optimistic-concurrency token. Send it back on an edit.</summary>
    public required int Revision { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
