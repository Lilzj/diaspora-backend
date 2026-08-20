using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Lifts a soft delete.
/// <para>
/// <b>Undelete is the one lifecycle act the aggregate must perform itself.</b> Deletion is
/// stamped by the persistence pipeline from a repository removal, but there is no "un-remove"
/// on a change tracker — restoring is an ordinary state change, and it raises the undeleted
/// event downstream re-indexing needs.
/// </para>
/// <para>
/// The guide is fetched through <see cref="IGuideRepository.GetIncludingDeletedAsync"/> — the one
/// read that sees past the shared context's global filter. Every other read is right to hide
/// deleted rows; this one has to load the row deletion hid.
/// </para>
/// </summary>
public sealed class UndeleteGuideCommandHandler
    : ICommandHandler<UndeleteGuideCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;

    public UndeleteGuideCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<GuideResponse>> Handle(
        UndeleteGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetIncludingDeletedAsync(
            GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        if (!guide.IsDeleted)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotDeleted);
        }

        guide.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
