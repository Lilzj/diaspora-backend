using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Takes a property off the market.
/// <para>
/// A temporary withdrawal becomes Unavailable and returns without republication, keeping the
/// listing age and its view count; an archive is the end of its public life and needs a deliberate
/// restore through draft. A landlord redecorating for a fortnight wants the first and is usually
/// offered only the second.
/// </para>
/// </summary>
public sealed class ArchiveHousingListingCommandHandler
    : ICommandHandler<ArchiveHousingListingCommand>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IClock _clock;

    public ArchiveHousingListingCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IClock clock)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ArchiveHousingListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure(listing.Error);
        }

        var now = _clock.UtcNow;
        var reason = command.Request?.Reason;

        var moved = command.Request?.Temporarily == true
            ? listing.Value.MakeUnavailable(now, _authorization.CallerId, reason)
            : listing.Value.Archive(now, _authorization.CallerId, reason);

        if (moved.IsFailure)
        {
            return moved;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
