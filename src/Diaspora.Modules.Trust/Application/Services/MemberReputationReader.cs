using Diaspora.Contracts.Trust.V1.Reputation;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Trust's answer to <see cref="IMemberReputationReader"/>: the reputation record and the active
/// badge awards, read without tracking.
/// <para>
/// <b>The same two reads the member's own routes make</b> — <c>GET /trust/reputation/me</c> and
/// <c>GET /trust/badges/me</c> — so a creator's reputation screen and the member's trust screen
/// cannot disagree about a level or a badge.
/// </para>
/// </summary>
public sealed class MemberReputationReader : IMemberReputationReader
{
    private readonly ITrustProfileRepository _profiles;
    private readonly IReputationProfileRepository _reputations;
    private readonly IClock _clock;

    public MemberReputationReader(
        ITrustProfileRepository profiles, IReputationProfileRepository reputations, IClock clock)
    {
        _profiles = profiles;
        _reputations = reputations;
        _clock = clock;
    }

    public async Task<MemberReputationSnapshot> GetAsync(
        Guid memberUserId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(memberUserId, Guid.Empty);

        var userId = UserId.From(memberUserId);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);
        var stored = await _reputations.GetByUserReadOnlyAsync(userId, cancellationToken);

        // Never staged or saved: an unsaved fresh record is only how Trust spells "Member on zero",
        // the same answer GET /trust/reputation/me gives a member with no scoring history.
        var reputation = stored ?? ReputationProfile.Open(userId, _clock.UtcNow);

        var badges = profile is null
            ? []
            : profile.Badges
                .Where(badge => badge.IsActive)
                .OrderBy(badge => badge.AwardedAt)
                .Select(badge => new MemberBadgeAward
                {
                    Type = badge.Badge.ToString(),
                    Label = BadgeLabels.For(badge.Badge, badge.CountryCode),
                    CountryCode = badge.CountryCode,
                    AwardedAt = badge.AwardedAt
                })
                .ToList();

        var next = reputation.NextLevel;

        var figures = new MemberReputationFigures
        {
            HasRecord = stored is not null,
            ContributionScore = reputation.ContributionScore,
            VerificationScore = reputation.VerificationScore,
            TrustScore = reputation.TrustScore,
            CombinedScore = reputation.CombinedScore,
            Level = reputation.Level.ToString(),
            NextLevel = next?.Level.ToString(),
            NextLevelMinimumScore = next?.MinScore,
            PointsToNextLevel = next?.PointsNeeded,
            GuidesApproved = reputation.GuidesApproved,
            CommunityConfirmationsReceived = reputation.CommunityConfirmationsReceived,
            LastCalculatedAt = reputation.LastCalculatedAt
        };

        return new MemberReputationSnapshot
        {
            UserId = memberUserId,
            Reputation = figures,
            Badges = badges
        };
    }
}
