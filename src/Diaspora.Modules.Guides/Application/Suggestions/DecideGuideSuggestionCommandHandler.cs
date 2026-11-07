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

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Applies the guide author's answer to a suggestion.
/// <para>
/// The guide is loaded to answer the question the suggestion cannot: whose guide is this. Only
/// its author decides — staff never answer for them — and that is the whole of the
/// authorization; the suggester gets the note either way.
/// </para>
/// </summary>
public sealed class DecideGuideSuggestionCommandHandler
    : ICommandHandler<DecideGuideSuggestionCommand, GuideSuggestionResponse>
{
    private readonly IGuideSuggestionRepository _suggestions;
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IClock _clock;

    public DecideGuideSuggestionCommandHandler(
        IGuideSuggestionRepository suggestions,
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IClock clock)
    {
        _suggestions = suggestions;
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<GuideSuggestionResponse>> Handle(
        DecideGuideSuggestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var suggestion = await _suggestions.GetAsync(
            GuideSuggestionId.From(command.SuggestionId), cancellationToken);

        if (suggestion is null)
        {
            return Result.Failure<GuideSuggestionResponse>(GuideErrors.SuggestionNotFound);
        }

        var guide = await _guides.GetForReadAsync(suggestion.GuideId, cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideSuggestionResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideSuggestionResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var note = command.Request.Note;

        var decided = command.Request.Accept
            ? suggestion.Accept(caller.Value, now, note)
            : suggestion.Reject(caller.Value, now, note);

        if (decided.IsFailure)
        {
            return Result.Failure<GuideSuggestionResponse>(decided.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideSuggestionProjection.ToResponse(suggestion));
    }
}
