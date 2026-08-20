using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Deletes a guide through the repository's removal path.
/// <para>
/// <b>The handler never stamps the deletion.</b> It marks the aggregate for removal and saves;
/// the shared <c>DbContext</c> converts that into a soft delete, sets the timestamp and the
/// actor, and leaves the row where every audit and every restore can still find it. Doing it by
/// hand here is precisely the defect B1.0.1 records — and it is silent, because a hand-stamped
/// delete looks correct in the response either way.
/// </para>
/// </summary>
public sealed class DeleteGuideCommandHandler : ICommandHandler<DeleteGuideCommand>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;

    public DeleteGuideCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        DeleteGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        _guides.Remove(guide);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
