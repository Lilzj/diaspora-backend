using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Publishing;

/// <summary>Records the instant a draft should publish itself at.</summary>
public sealed class SchedulePublicationCommandHandler
    : ICommandHandler<SchedulePublicationCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IClock _clock;

    public SchedulePublicationCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IClock clock)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<GuideResponse>> Handle(
        SchedulePublicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var scheduled = guide.SchedulePublication(command.Request.PublishAt, _clock.UtcNow);

        if (scheduled.IsFailure)
        {
            return Result.Failure<GuideResponse>(scheduled.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
