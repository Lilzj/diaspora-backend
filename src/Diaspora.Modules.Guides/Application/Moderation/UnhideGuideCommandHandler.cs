using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>Returns a hidden guide to public view. Idempotent.</summary>
public sealed class UnhideGuideCommandHandler : ICommandHandler<UnhideGuideCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;

    public UnhideGuideCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<GuideResponse>> Handle(
        UnhideGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var unhidden = guide.Unhide(caller.Value);

        if (unhidden.IsFailure)
        {
            return Result.Failure<GuideResponse>(unhidden.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide));
    }
}
