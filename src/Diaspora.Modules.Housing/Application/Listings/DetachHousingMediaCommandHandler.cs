using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Removes a media reference from a property.
/// <para>
/// Never deletes the asset. Whether an unreferenced asset is swept is Media's decision, and a
/// landlord who detaches a photograph often reattaches it a minute later.
/// </para>
/// </summary>
public sealed class DetachHousingMediaCommandHandler
    : ICommandHandler<DetachHousingMediaCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingListingReadContext _read;

    public DetachHousingMediaCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingListingReadContext read)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _read = read;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        DetachHousingMediaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(listing.Error);
        }

        var detached = listing.Value.DetachMedia(command.MediaId);

        if (detached.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(detached.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(listing.Value, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }
}
