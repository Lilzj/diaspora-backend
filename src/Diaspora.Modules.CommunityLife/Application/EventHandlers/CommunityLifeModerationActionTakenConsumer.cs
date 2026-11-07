using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Closes the event and organisation report rows a moderation decision answered (7 Nov 2026).
/// <para>
/// A hide or a removal upholds them, which pays the reporters through Trust; no action dismisses
/// them. A restore leaves them as they are: it answers the owner, not the reports. A hide the system
/// made on its own resolves nothing, because no person has looked yet.
/// </para>
/// <para>
/// One consumer for both content types: a module consumes an event once.
/// </para>
/// </summary>
public sealed class CommunityLifeModerationActionTakenConsumer
    : IntegrationEventHandler<ModerationActionTakenIntegrationEvent>
{
    private readonly ICommunityLifeReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CommunityLifeModerationActionTakenConsumer(
        ICommunityLifeReportRepository reports, IUnitOfWork unitOfWork, IClock clock)
    {
        _reports = reports;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        ModerationActionTakenIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        ReportSubjectType subjectType;

        switch (integrationEvent.ContentType)
        {
            case ModerationContentTypes.Event:
                subjectType = ReportSubjectType.Event;
                break;
            case ModerationContentTypes.Organization:
                subjectType = ReportSubjectType.Organization;
                break;
            default:
                return;
        }

        if (integrationEvent.DecidedByUserId is not { } decidedBy
            || integrationEvent.ModuleReportIds.Count == 0)
        {
            return;
        }

        CommunityLifeReportStatus outcome;

        switch (integrationEvent.Outcome)
        {
            case nameof(ModerationOutcome.HidePendingReview):
            case nameof(ModerationOutcome.Remove):
                outcome = CommunityLifeReportStatus.Upheld;
                break;
            case nameof(ModerationOutcome.NoAction):
                outcome = CommunityLifeReportStatus.Dismissed;
                break;
            default:
                return;
        }

        var now = _clock.UtcNow;
        var note = outcome == CommunityLifeReportStatus.Upheld
            ? $"Upheld: {integrationEvent.ReasonLabel}."
            : "Looked at and no action taken.";

        foreach (var reportId in integrationEvent.ModuleReportIds)
        {
            var report = await _reports.GetAsync(CommunityLifeReportId.From(reportId), cancellationToken);

            // Another module's report id, or a report about something else, is not this decision's.
            if (report is null
                || report.SubjectType != subjectType
                || report.SubjectId != integrationEvent.ContentId)
            {
                continue;
            }

            // Already resolved rows refuse quietly; a second decision on the case resolves nothing twice.
            report.Resolve(outcome, UserId.From(decidedBy), note, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
