using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>Relabels the guide's language.</summary>
public sealed class SetGuideLanguageCommandHandler
    : ICommandHandler<SetGuideLanguageCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;

    public SetGuideLanguageCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<GuideResponse>> Handle(
        SetGuideLanguageCommand command, CancellationToken cancellationToken)
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

        var language = GuideLanguage.Create(command.Request.Language);

        if (language.IsFailure)
        {
            return Result.Failure<GuideResponse>(language.Error);
        }

        var changed = guide.ChangeLanguage(language.Value, baseRevision: null);

        if (changed.IsFailure)
        {
            return Result.Failure<GuideResponse>(changed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
