using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Suggestions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Withdraws a suggestion on its author's own instruction.
/// <para>
/// No guide is loaded: the aggregate compares the caller against its own author, and the guide
/// has no say in whether somebody may retract what they wrote.
/// </para>
/// </summary>
public sealed class WithdrawGuideSuggestionCommandHandler
    : ICommandHandler<WithdrawGuideSuggestionCommand, GuideSuggestionResponse>
{
    private readonly IGuideSuggestionRepository _suggestions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IClock _clock;

    public WithdrawGuideSuggestionCommandHandler(
        IGuideSuggestionRepository suggestions,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IClock clock)
    {
        _suggestions = suggestions;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<GuideSuggestionResponse>> Handle(
        WithdrawGuideSuggestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideSuggestionResponse>(caller.Error);
        }

        var suggestion = await _suggestions.GetAsync(
            GuideSuggestionId.From(command.SuggestionId), cancellationToken);

        if (suggestion is null)
        {
            return Result.Failure<GuideSuggestionResponse>(GuideErrors.SuggestionNotFound);
        }

        var withdrawn = suggestion.Withdraw(caller.Value, _clock.UtcNow);

        if (withdrawn.IsFailure)
        {
            return Result.Failure<GuideSuggestionResponse>(withdrawn.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideSuggestionProjection.ToResponse(suggestion));
    }
}
