using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Everything about the reader that a visibility check or a feed needs.
/// <para>
/// Resolved <b>once per request</b> and reused. The lookups behind it cross three module
/// boundaries — Trust for verification, Location for residency, Community for follows and
/// blocks — and doing them per item is the difference between a feed page costing four queries
/// and costing eighty.
/// </para>
/// </summary>
public sealed record ViewerContext
{
    public UserId? UserId { get; init; }

    public bool IsAuthenticated => UserId is not null;

    /// <summary>From Trust, through the kernel's service. Never derived here.</summary>
    public bool IsVerified { get; init; }

    /// <summary>The platform trust level as an ordinal, for ranking.</summary>
    public int TrustLevel { get; init; }

    /// <summary>The platform access tier as an ordinal.</summary>
    public int Tier { get; init; }

    /// <summary>From Location. Countries Trust has verified this member as a resident of.</summary>
    public IReadOnlyCollection<string> VerifiedCountries { get; init; } = [];

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? HierarchyPath { get; init; }

    /// <summary>
    /// The reader's active location (their travel destination while travelling, else their
    /// current location), from Location. What a neighbourhood audience is measured from since
    /// 14 Oct 2026. Null when the reader has set none, or it has no point.
    /// </summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public bool HasPoint => Latitude is not null && Longitude is not null;

    /// <summary>Members this reader follows. Read by visibility and by ranking.</summary>
    public IReadOnlySet<Guid> FollowedMemberIds { get; init; } = new HashSet<Guid>();

    /// <summary>
    /// Members whose content this reader must not see.
    /// <para>
    /// Always empty today, and deliberately so: <b>blocking is Identity's</b>, and Identity
    /// publishes no contract for reading a member's block list. B6 threads the concept all the
    /// way through — the field, the feed filter, the visibility evaluator — so that the day
    /// Identity exposes it, one resolver method changes and every surface starts honouring it.
    /// Recorded as a missing platform contract in the platform evolution report.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<Guid> BlockedMemberIds { get; init; } = [];

    public static ViewerContext Anonymous { get; } = new();
}
