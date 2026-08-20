using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Authors;

/// <summary>
/// What Trust has said about one author, projected locally. Unique per user.
/// <para>
/// <b>Guides stores no verification logic</b> — the "Location owns the claim, Trust owns the
/// proof" pattern, applied to authors. This row is fed only by Trust integration events:
/// contributor approval, community-guide approval, badge awards and revocations, trust-level
/// changes. It exists so a guide detail page can render "Approved Contributor" without a
/// cross-module call per author per page.
/// </para>
/// <para>
/// <b>Every write is guarded on the event's timestamp</b>, per fact, because delivery is
/// at-least-once and unordered: a redelivered year-old "approved" must not overwrite last
/// week's removal, and a badge event racing a trust-level event must not suppress either. One
/// shared timestamp would create exactly that suppression, which is why each fact keeps its
/// own. Stale writes are dropped as success — the event was handled correctly.
/// </para>
/// </summary>
public sealed class GuideAuthorStanding : AggregateRoot<GuideAuthorStandingId>
{
    private readonly List<GuideAuthorBadge> _badges = [];

    private GuideAuthorStanding(GuideAuthorStandingId id, UserId userId)
        : base(id)
    {
        UserId = userId;
    }

    private GuideAuthorStanding() { }

    public UserId UserId { get; private set; } = default!;

    public bool IsApprovedContributor { get; private set; }

    public bool IsCommunityGuide { get; private set; }

    /// <summary>Trust's level label, opaque to this module. Rendered, never interpreted.</summary>
    public string? TrustLevel { get; private set; }

    public IReadOnlyList<GuideAuthorBadge> Badges => _badges.AsReadOnly();

    /// <summary>The newest event timestamp applied to any fact. For observability, not guarding.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>When the contributor fact last changed — the per-fact staleness guard.</summary>
    public DateTimeOffset? ContributorChangedAt { get; private set; }

    /// <summary>When the community-guide fact last changed.</summary>
    public DateTimeOffset? CommunityGuideChangedAt { get; private set; }

    /// <summary>When the trust level last changed.</summary>
    public DateTimeOffset? TrustLevelChangedAt { get; private set; }

    public static Result<GuideAuthorStanding> Create(GuideAuthorStandingId id, UserId userId)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        return Result.Success(new GuideAuthorStanding(id, userId));
    }

    /// <summary>Applies Trust's contributor decision. Stale events are dropped as success.</summary>
    public Result ApplyContributorApproval(bool isApproved, DateTimeOffset occurredAt)
    {
        if (ContributorChangedAt is { } current && occurredAt <= current)
        {
            return Result.Success();
        }

        IsApprovedContributor = isApproved;
        ContributorChangedAt = occurredAt;
        Advance(occurredAt);

        return Result.Success();
    }

    /// <summary>Applies Trust's community-guide decision. Stale events are dropped as success.</summary>
    public Result ApplyCommunityGuideApproval(bool isCommunityGuide, DateTimeOffset occurredAt)
    {
        if (CommunityGuideChangedAt is { } current && occurredAt <= current)
        {
            return Result.Success();
        }

        IsCommunityGuide = isCommunityGuide;
        CommunityGuideChangedAt = occurredAt;
        Advance(occurredAt);

        return Result.Success();
    }

    /// <summary>Applies Trust's level label. Stale events are dropped as success.</summary>
    public Result ApplyTrustLevel(string? trustLevel, DateTimeOffset occurredAt)
    {
        if (TrustLevelChangedAt is { } current && occurredAt <= current)
        {
            return Result.Success();
        }

        TrustLevel = string.IsNullOrWhiteSpace(trustLevel) ? null : trustLevel.Trim();
        TrustLevelChangedAt = occurredAt;
        Advance(occurredAt);

        return Result.Success();
    }

    /// <summary>
    /// Records an awarded badge. Idempotent on (badge, country) — a redelivery lands on the
    /// existing row, and a re-award after a newer revocation replaces it only when newer.
    /// </summary>
    public Result AwardBadge(string? badge, string? countryCode, DateTimeOffset awardedAt)
    {
        var name = badge?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return Result.Success();
        }

        var existing = _badges.Find(candidate => candidate.Matches(name, countryCode));

        if (existing is not null)
        {
            if (awardedAt <= existing.AwardedAt)
            {
                return Result.Success();
            }

            _badges.Remove(existing);
        }

        _badges.Add(GuideAuthorBadge.Create(name, countryCode, awardedAt));
        Advance(awardedAt);

        return Result.Success();
    }

    /// <summary>
    /// Removes a badge, unless the stored award is newer than the revocation — which under
    /// unordered delivery means the badge was re-awarded after this revocation was issued.
    /// </summary>
    public Result RevokeBadge(string? badge, string? countryCode, DateTimeOffset revokedAt)
    {
        var name = badge?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return Result.Success();
        }

        var existing = _badges.Find(candidate => candidate.Matches(name, countryCode));

        if (existing is null || existing.AwardedAt > revokedAt)
        {
            return Result.Success();
        }

        _badges.Remove(existing);
        Advance(revokedAt);

        return Result.Success();
    }

    private void Advance(DateTimeOffset occurredAt)
    {
        if (UpdatedAt is null || occurredAt > UpdatedAt)
        {
            UpdatedAt = occurredAt;
        }
    }
}
