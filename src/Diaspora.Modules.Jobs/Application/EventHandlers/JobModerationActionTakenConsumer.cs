using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// Closes the jobs report rows a moderation decision answered (7 Nov 2026).
/// <para>
/// A hide or a removal upholds them, which pays the reporters through Trust; no action dismisses them.
/// A restore leaves them as they are: it answers the owner, not the reports. A hide the system made
/// on its own resolves nothing, because no person has looked yet.
/// </para>
/// </summary>
public sealed class JobModerationActionTakenConsumer
    : IntegrationEventHandler<ModerationActionTakenIntegrationEvent>
{
    private readonly IJobReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public JobModerationActionTakenConsumer(
        IJobReportRepository reports, IUnitOfWork unitOfWork, IClock clock)
    {
        _reports = reports;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        ModerationActionTakenIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ContentType != ModerationContentTypes.Job
            || integrationEvent.DecidedByUserId is not { } decidedBy
            || integrationEvent.ModuleReportIds.Count == 0)
        {
            return;
        }

        bool upheld;

        switch (integrationEvent.Outcome)
        {
            case nameof(ModerationOutcome.HidePendingReview):
            case nameof(ModerationOutcome.Remove):
                upheld = true;
                break;
            case nameof(ModerationOutcome.NoAction):
                upheld = false;
                break;
            default:
                return;
        }

        var now = _clock.UtcNow;
        var resolution = upheld
            ? $"Upheld: {integrationEvent.ReasonLabel}."
            : "Looked at and no action taken.";

        foreach (var reportId in integrationEvent.ModuleReportIds)
        {
            var report = await _reports.GetAsync(JobReportId.From(reportId), cancellationToken);

            if (report is null || report.PostingId.Value != integrationEvent.ContentId)
            {
                continue;
            }

            // Already resolved rows refuse quietly; a second decision on the case resolves nothing twice.
            report.Resolve(UserId.From(decidedBy), upheld, resolution, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
