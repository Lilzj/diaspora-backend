using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Suggestions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Records an improvement proposal against a guide.
/// <para>
/// The guide is loaded read-only and never mutated — a suggestion is a separate aggregate, so
/// a stranger proposing a fix cannot bump the revision an author is mid-save against. Only
/// published, visible guides can be suggested to, because a suggestion against a draft is a
/// comment on work in progress that the author has not asked for.
/// </para>
/// </summary>
public sealed class SubmitGuideSuggestionCommandHandler
    : ICommandHandler<SubmitGuideSuggestionCommand, GuideSuggestionResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideSuggestionRepository _suggestions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitGuideSuggestionCommandHandler(
        IGuideRepository guides,
        IGuideSuggestionRepository suggestions,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _guides = guides;
        _suggestions = suggestions;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GuideSuggestionResponse>> Handle(
        SubmitGuideSuggestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideSuggestionResponse>(caller.Error);
        }

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideSuggestionResponse>(GuideErrors.NotFound);
        }

        if (guide.AuthorId == caller.Value)
        {
            return Result.Failure<GuideSuggestionResponse>(GuideErrors.SuggestionSelf);
        }

        var suggestion = GuideSuggestion.Submit(
            GuideSuggestionId.From(_guids.NewSequentialGuid()),
            guideId,
            caller.Value,
            command.Request.ProposedContent,
            _clock.UtcNow,
            command.Request.TargetSectionId,
            command.Request.Rationale);

        if (suggestion.IsFailure)
        {
            return Result.Failure<GuideSuggestionResponse>(suggestion.Error);
        }

        await _suggestions.AddAsync(suggestion.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideSuggestionProjection.ToResponse(suggestion.Value));
    }
}
