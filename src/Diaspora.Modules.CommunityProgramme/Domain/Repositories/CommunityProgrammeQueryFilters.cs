using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Domain.Repositories;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// What each page is being asked for.
//
// A record per query rather than a wall of optional repository parameters, for the reason B7 first
// recorded: a repository method with eleven nullable arguments is a method every caller passes nulls
// into positionally, and the first one that transposes two of them fails silently.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>What a membership-programme discovery page is being asked for.</summary>
public sealed record ProgrammeQueryFilter
{
    /// <summary>
    /// Which organizations may appear at all.
    /// <para>
    /// <b>Resolved before the query and applied inside it.</b> The caller's own organizations are
    /// looked up once through B12's directory, and a private programme belonging to a body the caller
    /// is not in never enters the result set — rather than entering it and being filtered out, which
    /// would mean it had already been read.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid>? VisibleOrganizationIds { get; init; }

    /// <summary>The organizations the discovery page is narrowed to, after B12 has matched on text.</summary>
    public IReadOnlyList<Guid>? CandidateOrganizationIds { get; init; }

    /// <summary>Free/paid. Applied over the tiers, so a mixed programme matches both bands.</summary>
    public string? PriceBand { get; init; }

    /// <summary>Only programmes currently taking members.</summary>
    public bool OpenOnly { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityProgrammeLimits.DefaultPageSize;
}

/// <summary>What an organization's member roll is being asked for.</summary>
public sealed record MembershipRollFilter
{
    public required Guid OrganizationId { get; init; }

    public IReadOnlyList<ProgrammeMembershipStatus>? Statuses { get; init; }

    public MembershipTierId? TierId { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityProgrammeLimits.DefaultPageSize;
}

/// <summary>What a volunteer-opportunity page is being asked for.</summary>
public sealed record OpportunityQueryFilter
{
    public string? SearchTerm { get; init; }

    public string? Cause { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? Skill { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public IReadOnlyList<VolunteerOpportunityStatus>? Statuses { get; init; }

    /// <summary>
    /// Organizations whose drafts the caller may see.
    /// <para>
    /// Empty for an anonymous browse, which is what keeps an unpublished opportunity out of a
    /// stranger's list without a second query and without a post-filter.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid>? ManagedOrganizationIds { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityProgrammeLimits.DefaultPageSize;
}

/// <summary>What an applicant queue is being asked for.</summary>
public sealed record ApplicationQueryFilter
{
    public VolunteerOpportunityId? OpportunityId { get; init; }

    public Guid? OrganizationId { get; init; }

    public UserId? VolunteerUserId { get; init; }

    public IReadOnlyList<VolunteerApplicationStatus>? Statuses { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityProgrammeLimits.DefaultPageSize;
}

/// <summary>What a poll page is being asked for.</summary>
public sealed record PollQueryFilter
{
    public Guid? OrganizationId { get; init; }

    public PollAudience? Audience { get; init; }

    public IReadOnlyList<PollStatus>? Statuses { get; init; }

    /// <summary>Legacy catalogue area. Matches only polls drafted before 14 Oct 2026.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>A town by name, compared case-insensitively (14 Oct 2026).</summary>
    public string? CityName { get; init; }

    /// <summary>
    /// The organizations whose members the caller is one of.
    /// <para>
    /// A members-only poll appears in a list only for somebody in the body, which is the same
    /// server-side rule the detail read enforces with a 403. Filtering the list on the device would
    /// mean the private question had already been sent to it.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid>? MemberOfOrganizationIds { get; init; }

    /// <summary>The caller, so an invitation-only poll they were named on appears.</summary>
    public UserId? ViewerUserId { get; init; }

    /// <summary>Whether the caller may see drafts, and of which organizations.</summary>
    public IReadOnlyList<Guid>? ManagedOrganizationIds { get; init; }

    public string? SearchTerm { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityProgrammeLimits.DefaultPageSize;
}

/// <summary>What an announcement feed is being asked for.</summary>
public sealed record AnnouncementQueryFilter
{
    public AnnouncementScope? Scope { get; init; }

    public Guid? ScopeId { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>
    /// The organizations the caller belongs to.
    /// <para>
    /// An organization announcement reaches its members and nobody else — the audience-resolution
    /// rule the register asks for, applied in the query rather than after it.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid>? MemberOfOrganizationIds { get; init; }

    public string? SearchTerm { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityProgrammeLimits.DefaultPageSize;
}
