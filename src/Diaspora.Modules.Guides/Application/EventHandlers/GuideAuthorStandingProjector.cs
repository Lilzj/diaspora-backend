using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Projects Trust's facts about a member onto their author standing.
/// <para>
/// <b>"Location owns the claim, Trust owns the proof", applied to people.</b> B8 stores no
/// verification logic about authors and grants no contributor status: it keeps a read model of
/// what Trust decided, fed exclusively by Trust's published events, so a guide card can render a
/// badge without a cross-module call on every page.
/// </para>
/// <para>
/// <b>Get-or-create per member.</b> A member's first Trust event is often about somebody who has
/// never written a guide — a contributor approval usually precedes the first draft — so the
/// standing row is created on demand. The unique user index arbitrates the race between two
/// events about the same author arriving at once.
/// </para>
/// <para>
/// <b>Every write is guarded on the event's timestamp</b>, inside the aggregate, because delivery
/// is at-least-once and unordered. A redelivered badge award from last month must not resurrect a
/// badge revoked last week, and the aggregate's per-fact timestamps are what make that decision
/// per fact rather than per row.
/// </para>
/// </summary>
public sealed class GuideAuthorStandingProjector
{
    private readonly IGuideAuthorStandingRepository _standings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly ILogger<GuideAuthorStandingProjector> _logger;

    public GuideAuthorStandingProjector(
        IGuideAuthorStandingRepository standings,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        ILogger<GuideAuthorStandingProjector> logger)
    {
        _standings = standings;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _logger = logger;
    }

    /// <summary>Trust approved — or withdrew — the right to publish guides.</summary>
    public Task ApplyContributorApprovalAsync(
        Guid userId,
        bool isApproved,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            userId,
            standing => standing.ApplyContributorApproval(isApproved, occurredAt),
            cancellationToken);

    /// <summary>Trust recognised the member as a community guide.</summary>
    public Task ApplyCommunityGuideApprovalAsync(
        Guid userId,
        bool isCommunityGuide,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            userId,
            standing => standing.ApplyCommunityGuideApproval(isCommunityGuide, occurredAt),
            cancellationToken);

    /// <summary>Trust moved the member's level. Stored as a label, never interpreted here.</summary>
    public Task ApplyTrustLevelAsync(
        Guid userId,
        string? trustLevel,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            userId,
            standing => standing.ApplyTrustLevel(trustLevel, occurredAt),
            cancellationToken);

    public Task AwardBadgeAsync(
        Guid userId,
        string badge,
        string? countryCode,
        DateTimeOffset awardedAt,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            userId,
            standing => standing.AwardBadge(badge, countryCode, awardedAt),
            cancellationToken);

    public Task RevokeBadgeAsync(
        Guid userId,
        string badge,
        string? countryCode,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            userId,
            standing => standing.RevokeBadge(badge, countryCode, revokedAt),
            cancellationToken);

    /// <summary>
    /// Loads or creates the member's standing, applies one fact, and commits.
    /// <para>
    /// Saved here because an integration event handler runs out of band, in its own scope,
    /// outside any command's unit of work — nothing else is going to commit this.
    /// </para>
    /// </summary>
    private async Task MutateAsync(
        Guid userId,
        Func<GuideAuthorStanding, SharedKernel.Results.Result> mutate,
        CancellationToken cancellationToken)
    {
        var memberId = UserId.From(userId);
        var standing = await _standings.GetByUserAsync(memberId, cancellationToken);

        if (standing is null)
        {
            var created = GuideAuthorStanding.Create(
                GuideAuthorStandingId.From(_guids.NewSequentialGuid()), memberId);

            if (created.IsFailure)
            {
                _logger.LogWarning(
                    "Creating author standing for member {UserId} failed: {Error}.",
                    userId,
                    created.Error.Code);

                return;
            }

            standing = created.Value;
            await _standings.AddAsync(standing, cancellationToken);
        }

        var applied = mutate(standing);

        if (applied.IsFailure)
        {
            _logger.LogWarning(
                "Applying a Trust fact to author standing for member {UserId} failed: {Error}.",
                userId,
                applied.Error.Code);

            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
