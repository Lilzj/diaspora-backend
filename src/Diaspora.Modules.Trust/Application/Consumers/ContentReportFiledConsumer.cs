using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Moderation;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Trust.Application.Consumers;

/// <summary>
/// Turns a member's report, from any module, into a moderation case (7 Nov 2026).
/// <para>
/// <b>The first thing that ever fed the queue.</b> Reports went to five module tables and Trust's
/// queue was empty. Now every report lands on the one open case for that content: a fraud or scam
/// report lists it at once, anything else when a second member reports it, and many serious reports
/// hide it pending review, with the owner told at once.
/// </para>
/// </summary>
public sealed class ContentReportFiledConsumer
    : IntegrationEventHandler<ContentReportFiledIntegrationEvent>
{
    private readonly IModerationItemRepository _cases;
    private readonly ModerationTargetRegistry _targets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ContentReportFiledConsumer> _logger;

    public ContentReportFiledConsumer(
        IModerationItemRepository cases,
        ModerationTargetRegistry targets,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ContentReportFiledConsumer> logger)
    {
        _cases = cases;
        _targets = targets;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        ContentReportFiledIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ReporterUserId == integrationEvent.ContentOwnerUserId)
        {
            return;
        }

        var target = _targets.Find(integrationEvent.ContentType);

        if (target is null)
        {
            _logger.LogWarning(
                "A {Module} report about {ContentType} {ContentId} reached Trust, and no module "
                + "moderates that content type. The report stays in the module's own table.",
                integrationEvent.Module, integrationEvent.ContentType, integrationEvent.ContentId);
            return;
        }

        var code = ModerationReasons.TryParse(integrationEvent.ReasonCode, out var parsed)
            ? parsed
            : ModerationReasonCode.Other;

        var item = await _cases.GetOpenForTargetAsync(
            integrationEvent.ContentType, integrationEvent.ContentId.ToString(), cancellationToken);

        if (item is null)
        {
            item = ModerationItem.OpenForReports(
                integrationEvent.ContentType,
                target.Area,
                integrationEvent.ContentId,
                UserId.From(integrationEvent.ContentOwnerUserId),
                integrationEvent.ContentTitle,
                _clock.UtcNow);

            await _cases.AddAsync(item, cancellationToken);
        }

        var added = item.AddReport(
            UserId.From(integrationEvent.ReporterUserId),
            code,
            integrationEvent.ModuleReason,
            integrationEvent.Detail,
            integrationEvent.Module,
            integrationEvent.ModuleReportId,
            integrationEvent.FiledAt);

        if (added && item.ShouldHideAutomatically())
        {
            // The module acts first; the case records only a hide that happened, so the owner is
            // never told about one that did not.
            var applied = await target.ApplyAsync(
                integrationEvent.ContentId,
                ModerationOutcome.HidePendingReview,
                new ModerationActor(
                    null,
                    item.Id.Value,
                    item.ReasonCode ?? code,
                    ModerationItem.AutomaticHideNote(item.Reports.Count)),
                cancellationToken);

            if (applied.IsSuccess)
            {
                item.HideAutomatically(_clock.UtcNow);
            }
            else
            {
                _logger.LogWarning(
                    "The automatic hide of {ContentType} {ContentId} was refused by its module: {Error}. "
                    + "The case stays at the top of the queue for a person.",
                    integrationEvent.ContentType, integrationEvent.ContentId, applied.Error.Description);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
