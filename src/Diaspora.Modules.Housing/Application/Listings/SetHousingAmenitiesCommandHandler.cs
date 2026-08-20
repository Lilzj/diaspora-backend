using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Replaces a property amenity list.
/// <para>
/// Every key is checked against the catalogue, and a retired amenity is refused on write while
/// staying visible on the listings that already claim it. A landlord cannot newly tick something
/// the deployment has retired, and nobody loses a fact about a home that is still true.
/// </para>
/// </summary>
public sealed class SetHousingAmenitiesCommandHandler
    : ICommandHandler<SetHousingAmenitiesCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingCatalogResolver _catalog;
    private readonly HousingListingReadContext _read;

    public SetHousingAmenitiesCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingCatalogResolver catalog,
        HousingListingReadContext read)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _catalog = catalog;
        _read = read;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        SetHousingAmenitiesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(listing.Error);
        }

        var amenities = await _catalog.ResolveAmenitiesAsync(
            command.Request.Amenities, cancellationToken);

        if (amenities.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(amenities.Error);
        }

        var applied = listing.Value.SetAmenities(
            amenities.Value, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(listing.Value, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }
}
