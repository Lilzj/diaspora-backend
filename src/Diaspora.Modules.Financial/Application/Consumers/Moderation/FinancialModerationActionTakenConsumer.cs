using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Consumers.Moderation;

/// <summary>
/// Pauses and resumes the paid placements over content a moderation decision took out of view or
/// put back (7 Nov 2026).
/// <para>
/// <b>Pause, never cancel.</b> A hide pending review or a removal pauses the subject's running and
/// scheduled placements, banking the days they have left; a restore, or a case closed with no
/// action, resumes the ones a decision paused, with those days. A placement the buyer paused
/// themselves is never resumed by a moderator. Forfeiting a placement once a removal stands is a
/// later stage's, and nothing here cancels or refunds anything.
/// </para>
/// <para>
/// <b>The module's one reaction to a moderation decision.</b> Financial takes no reports, so there
/// are no report rows to resolve; if fundraisers ever take reports, resolving them belongs here
/// too, because a module consumes a published fact once.
/// </para>
/// <para>
/// Idempotent: a placement already paused is not paused again and one not paused by a decision is
/// not resumed, so a redelivered event, or a removal confirmed after its hide, changes nothing.
/// </para>
/// </summary>
public sealed class FinancialModerationActionTakenConsumer
    : IntegrationEventHandler<ModerationActionTakenIntegrationEvent>
{
    private readonly IPlacementRepository _placements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<FinancialModerationActionTakenConsumer> _logger;

    public FinancialModerationActionTakenConsumer(
        IPlacementRepository placements,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<FinancialModerationActionTakenConsumer> logger)
    {
        _placements = placements;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        ModerationActionTakenIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (ModerationPlacementSubjects.Map(integrationEvent.ContentType) is not { } subjectType
            || integrationEvent.ContentId == Guid.Empty
            || !Enum.TryParse<ModerationOutcome>(integrationEvent.Outcome, out var outcome))
        {
            return;
        }

        var subject = SubjectReference.Create(subjectType, integrationEvent.ContentId);
        var now = _clock.UtcNow;
        var changed = 0;

        switch (outcome)
        {
            case ModerationOutcome.HidePendingReview:
            case ModerationOutcome.Remove:
                foreach (var placement in await _placements.ListRunningOrScheduledForSubjectAsync(
                             subject, cancellationToken))
                {
                    if (placement.PauseForModeration(integrationEvent.CaseId, now))
                    {
                        _placements.Update(placement);
                        changed++;
                    }
                }

                break;

            case ModerationOutcome.Restore:
            case ModerationOutcome.NoAction:
                foreach (var placement in await _placements.ListPausedForModerationAsync(
                             subject, cancellationToken))
                {
                    // Restored content is back in view whichever case hid it. A case closed with no
                    // action hid nothing itself, so it lifts only a pause it made.
                    if (outcome is ModerationOutcome.NoAction
                        && placement.ModerationCaseId != integrationEvent.CaseId)
                    {
                        continue;
                    }

                    if (placement.ResumeAfterModeration(now))
                    {
                        _placements.Update(placement);
                        changed++;
                    }
                }

                break;

            default:
                return;
        }

        if (changed == 0)
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Moderation case {CaseId} ({Outcome}) {Action} {Count} placement(s) over {Subject}.",
            integrationEvent.CaseId,
            outcome,
            outcome is ModerationOutcome.HidePendingReview or ModerationOutcome.Remove ? "paused" : "resumed",
            changed,
            subject);
    }
}
