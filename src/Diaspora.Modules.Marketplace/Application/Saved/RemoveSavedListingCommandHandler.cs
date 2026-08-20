using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Saved;

/// <summary>
/// Unsaves a listing.
/// <para>
/// <b>The aggregate raises its removal event before the row goes.</b> A hard delete cannot raise
/// one, and without it the listing's save count would only ever go up — which is B1.0.1's lesson in
/// a different shape.
/// </para>
/// <para>
/// Unsaving something that was never saved succeeds. A client retrying a removal should not get an
/// error for reaching the state it asked for.
/// </para>
/// </summary>
public sealed class RemoveSavedListingCommandHandler : ICommandHandler<RemoveSavedListingCommand>
{
    private readonly ISavedListingRepository _saved;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;

    public RemoveSavedListingCommandHandler(
        ISavedListingRepository saved,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization)
    {
        _saved = saved;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        RemoveSavedListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var existing = await _saved.FindAsync(
            caller.Value,
            MarketplaceListingId.From(command.ListingId),
            cancellationToken);

        if (existing is null)
        {
            return Result.Success();
        }

        // Raised before the row goes. See the class summary.
        existing.NoteRemoved();

        _saved.Remove(existing);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
