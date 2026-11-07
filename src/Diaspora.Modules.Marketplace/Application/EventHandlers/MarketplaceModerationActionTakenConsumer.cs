using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Closes the listing report rows a moderation decision answered (7 Nov 2026).
/// <para>
/// A hide or a removal actions them, which pays the reporters through Trust; no action dismisses
/// them. A restore leaves them as they are: it answers the seller, not the reports. A hide the
/// system made on its own resolves nothing, because no person has looked yet.
/// </para>
/// </summary>
public sealed class MarketplaceModerationActionTakenConsumer
    : IntegrationEventHandler<ModerationActionTakenIntegrationEvent>
{
    private readonly IListingReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MarketplaceModerationActionTakenConsumer(
        IListingReportRepository reports, IUnitOfWork unitOfWork, IClock clock)
    {
        _reports = reports;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        ModerationActionTakenIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ContentType != ModerationContentTypes.MarketplaceListing
            || integrationEvent.DecidedByUserId is not { } decidedBy
            || integrationEvent.ModuleReportIds.Count == 0)
        {
            return;
        }

        ListingReportStatus status;

        switch (integrationEvent.Outcome)
        {
            case nameof(ModerationOutcome.HidePendingReview):
            case nameof(ModerationOutcome.Remove):
                status = ListingReportStatus.Actioned;
                break;
            case nameof(ModerationOutcome.NoAction):
                status = ListingReportStatus.Dismissed;
                break;
            default:
                return;
        }

        var now = _clock.UtcNow;
        var resolution = status == ListingReportStatus.Actioned
            ? $"Upheld: {integrationEvent.ReasonLabel}."
            : "Looked at and no action taken.";

        foreach (var reportId in integrationEvent.ModuleReportIds)
        {
            var report = await _reports.GetAsync(ListingReportId.From(reportId), cancellationToken);

            if (report is null || report.ListingId.Value != integrationEvent.ContentId)
            {
                continue;
            }

            // Already resolved rows refuse quietly; a second decision on the case resolves nothing twice.
            report.Resolve(status, UserId.From(decidedBy), resolution, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
