using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Moderation;

/// <summary>
/// What a removal costs the owner, applied once it stands (7 Nov 2026).
/// <para>
/// <b>Never before.</b> A removal stands when its 14-day appeal window closes with no appeal, or when
/// the appeal is not upheld. Until then nothing is taken: penalties used to land the moment a
/// moderator clicked, before anybody could answer.
/// </para>
/// <para>
/// What it costs: the defined reputation penalty for removed content, once; the next step on the
/// owner's strike ladder (a warning, then a week without posting in that area, then a month, then a
/// suspension two staff must confirm); a risk signal; the item's paused paid placements are forfeited
/// (Financial, from the event). The owner is told all of it.
/// </para>
/// </summary>
public sealed class ModerationConsequences
{
    private readonly ReputationService _reputation;
    private readonly IStrikeRepository _strikes;
    private readonly ITrustEventRepository _timeline;
    private readonly MemberRiskAssessor _risk;
    private readonly IIntegrationEventPublisher _events;

    public ModerationConsequences(
        ReputationService reputation,
        IStrikeRepository strikes,
        ITrustEventRepository timeline,
        MemberRiskAssessor risk,
        IIntegrationEventPublisher events)
    {
        _reputation = reputation;
        _strikes = strikes;
        _timeline = timeline;
        _risk = risk;
        _events = events;
    }

    /// <summary>Applies a standing removal's consequences. The caller saves.</summary>
    public async Task ApplyAsync(
        ModerationItem item, bool afterAppeal, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.ConsequencesAppliedAt is not null || item.CurrentOutcome is not ModerationOutcome.Remove)
        {
            return;
        }

        var owner = item.AuthorId;
        var code = item.ProposedReasonCode ?? item.ReasonCode ?? ModerationReasonCode.Other;

        var scored = await _reputation.ScoreAsync(
            owner,
            ReputationActions.ContentRemoved,
            ReputationAwardKey.Moderation(ReputationActions.ContentRemoved, item.Id.Value),
            item.TargetType ?? item.ContentType.ToString(),
            item.ContentId,
            cancellationToken: cancellationToken);

        var strike = await _strikes.GetForCaseAsync(item.Id, cancellationToken);

        if (strike is null)
        {
            var history = await _strikes.ListForMemberAsync(owner, cancellationToken);
            var active = history.Count(existing => existing.IsActive(now));

            strike = Strike.Issue(owner, item.Id, item.Area ?? "general", active, now);
            await _strikes.AddAsync(strike, cancellationToken);
        }

        if (Guid.TryParse(item.ContentId, out var contentId))
        {
            var report = item.Reports.FirstOrDefault();

            await _risk.RecordAsync(
                RiskSignal.ReportUpheld(
                    owner,
                    report?.Module ?? item.Area ?? "moderation",
                    item.TargetType ?? item.ContentType.ToString(),
                    contentId,
                    report?.ModuleReportId ?? item.Id.Value,
                    now),
                cancellationToken);
        }

        await _timeline.AddAsync(
            TrustEvent.Record(
                owner,
                TrustEventTypes.Violation,
                strike.Level switch
                {
                    StrikeLevel.Warning => "Warning: content removed",
                    StrikeLevel.First => "First strike: content removed",
                    StrikeLevel.Second => "Second strike: content removed",
                    _ => "Third strike: content removed"
                },
                now,
                $"Removed for being {ModerationReasons.Label(code)}.",
                TrustEventTypes.IconFor(TrustEventTypes.Violation)),
            cancellationToken);

        item.MarkConsequencesApplied(now);

        if (Guid.TryParse(item.ContentId, out var id))
        {
            await _events.PublishAsync(
                new ModerationDecisionStoodIntegrationEvent
                {
                    CaseId = item.Id.Value,
                    ContentType = item.TargetType ?? item.ContentType.ToString(),
                    ContentId = id,
                    ContentTitle = item.Title,
                    Area = item.Area ?? "general",
                    OwnerUserId = owner.Value,
                    ReasonLabel = ModerationReasons.Label(code),
                    StrikeLevel = strike.Level.ToString(),
                    BlockedUntil = strike.BlockedUntil,
                    PointsLost = scored.WasApplied ? Math.Abs(scored.Points) : 0,
                    AfterAppeal = afterAppeal,
                    StoodAt = now
                },
                cancellationToken);
        }

        if (strike.Level is StrikeLevel.Third)
        {
            await _events.PublishAsync(
                new MemberReachedThirdStrikeIntegrationEvent
                {
                    UserId = owner.Value,
                    CaseId = item.Id.Value,
                    Area = strike.Area,
                    ReasonCode = code.ToString(),
                    At = now
                },
                cancellationToken);
        }
    }
}
