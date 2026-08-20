using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Bumps the share counter on the guide and, when one exists, on the analytics row.
/// <para>
/// The analytics row is not created for a share. A guide nobody has ever opened cannot have
/// been shared in any way worth a row of its own, and creating one here would put an
/// all-zeroes record behind every scripted request.
/// </para>
/// </summary>
public sealed class ShareGuideCommandHandler : ICommandHandler<ShareGuideCommand>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;

    public ShareGuideCommandHandler(
        IGuideRepository guides,
        IGuideAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
