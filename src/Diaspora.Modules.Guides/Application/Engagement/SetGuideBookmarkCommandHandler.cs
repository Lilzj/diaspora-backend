using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Saves or unsaves a guide, through Community.
/// <para>
/// The guide is read first purely to confirm it exists and the caller may see it — saving a
/// guide that was hidden five minutes ago would create a bookmark the saved-collections screen
/// can never render, and Community has no way to know that on its own.
/// </para>
/// <para>
/// The guide's own bookmark counter is deliberately not touched here. Community owns the fact,
/// and the rollup is applied from its event — a handler that incremented the counter itself
/// would double-count the moment that event arrived.
/// </para>
/// </summary>
public sealed class SetGuideBookmarkCommandHandler : ICommandHandler<SetGuideBookmarkCommand>
{
    private readonly IGuideRepository _guides;
    private readonly GuideBookmarkService _bookmarks;
    private readonly GuideAuthorizationService _authorization;

    public SetGuideBookmarkCommandHandler(
        IGuideRepository guides,
        GuideBookmarkService bookmarks,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _bookmarks = bookmarks;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        SetGuideBookmarkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure(GuideErrors.NotFound);
        }

        return command.IsSaved
            ? await _bookmarks.SaveAsync(guideId, caller.Value, cancellationToken)
            : await _bookmarks.RemoveAsync(guideId, caller.Value, cancellationToken);
    }
}
