using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Tags;

/// <summary>Hands the whole tag set to the aggregate, which normalises and deduplicates it.</summary>
public sealed class SetGuideTagsCommandHandler : ICommandHandler<SetGuideTagsCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;

    public SetGuideTagsCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<GuideResponse>> Handle(
        SetGuideTagsCommand command, CancellationToken cancellationToken)
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

        var current = guide.EnsureRevision(command.Request.BaseRevision);

        if (current.IsFailure)
        {
            return Result.Failure<GuideResponse>(current.Error);
        }

        var tagged = guide.SetTags([.. command.Request.Tags], baseRevision: null);

        if (tagged.IsFailure)
        {
            return Result.Failure<GuideResponse>(tagged.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
