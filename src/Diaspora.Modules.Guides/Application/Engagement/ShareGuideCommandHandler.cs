using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.EventHandlers;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Bumps the share counter on the guide and, when one exists, on the analytics row — and tells the
/// platform's analytics.
/// <para>
/// The analytics row is not created for a share. A guide nobody has ever opened cannot have
/// been shared in any way worth a row of its own, and creating one here would put an
/// all-zeroes record behind every scripted request.
/// </para>
/// <para>
/// <b>The counter alone was invisible to the platform.</b> Until 13 Sep 2026 this handler only
/// moved the guide's own counters, and the Analytics module — which counts shares from events —
/// never learned of a guide share: a creator's "Shares" read zero however often their guides were
/// shared. It now publishes <c>GuideSharedIntegrationEvent</c>, enqueued before the save so the
/// counter and its announcement commit together or not at all.
/// </para>
/// </summary>
public sealed class ShareGuideCommandHandler : ICommandHandler<ShareGuideCommand>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideIntegrationEventRelay _relay;
    private readonly IClock _clock;

    public ShareGuideCommandHandler(
        IGuideRepository guides,
        IGuideAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideIntegrationEventRelay relay,
        IClock clock)
    {
        _guides = guides;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _relay = relay;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ShareGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure(GuideErrors.NotFound);
        }

        var analytics = await _analytics.GetByGuideAsync(guideId, cancellationToken);

        GuideEngagementService.RecordShare(guide, analytics);

        await _relay.PublishSharedAsync(
            guide.Id.Value,
            guide.AuthorId.Value,
            _authorization.CallerId?.Value,
            _clock.UtcNow,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
