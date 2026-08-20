using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Soft-deletes a property.
/// <para>
/// Soft only, and never hard. A deleted listing keeps its slug so shared links never resolve to
/// somebody else home, keeps its analytics so neighbourhood averages stay honest, and keeps its
/// reservation history because somebody may still be living there under an arrangement it records.
/// </para>
/// </summary>
public sealed class DeleteHousingListingCommandHandler
    : ICommandHandler<DeleteHousingListingCommand>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IClock _clock;

    public DeleteHousingListingCommandHandler(
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
        DeleteHousingListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure(listing.Error);
        }

        listing.Value.MarkDeleted(_clock.UtcNow, _authorization.CallerId?.Value.ToString());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
