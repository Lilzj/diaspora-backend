using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Moves the guide's view counter and the analytics row together.
/// <para>
/// The recorder reports the milestone a view crossed — ten, a hundred, a thousand — so the
/// platform can say something once rather than once per reader. Translating that into a
/// published fact belongs to the integration relay, not here: a handler that published its own
/// integration events would be a second translation seam, and two seams disagree.
/// </para>
/// </summary>
public sealed class RecordGuideViewCommandHandler : ICommandHandler<RecordGuideViewCommand>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAnalyticsRecorder _analytics;
    private readonly IClock _clock;

    public RecordGuideViewCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideAnalyticsRecorder analytics,
        IClock clock)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<Result> Handle(
        RecordGuideViewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure(GuideErrors.NotFound);
        }

        var request = command.Request;
        var unique = request.IsUniqueView ?? false;

        guide.RecordView(unique);

        await _analytics.RecordViewAsync(guideId, unique, _clock.UtcNow, cancellationToken);

        if (request.ReadSeconds is { } seconds)
        {
            // A completion carries its read time with it; a partial read reports the time on
            // its own. Recording both for a completion would count the seconds twice.
            if (request.Completed == true)
            {
                await _analytics.RecordCompletionAsync(guideId, seconds, cancellationToken);
            }
            else
            {
                await _analytics.RecordReadTimeAsync(guideId, seconds, cancellationToken);
            }
        }
        else if (request.Completed == true)
        {
            await _analytics.RecordCompletionAsync(guideId, 0, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
