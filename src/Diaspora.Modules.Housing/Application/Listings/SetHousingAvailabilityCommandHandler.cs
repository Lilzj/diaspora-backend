using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Restates when a property is available and how many units it has.
/// <para>
/// The window and the unit count are applied separately because they fail for different reasons and
/// a landlord correcting a move-in date should not be told about their bed count.
/// </para>
/// </summary>
public sealed class SetHousingAvailabilityCommandHandler
    : ICommandHandler<SetHousingAvailabilityCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingListingReadContext _read;

    public SetHousingAvailabilityCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingListingReadContext read)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _read = read;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        SetHousingAvailabilityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(loaded.Error);
        }

        var listing = loaded.Value;
        var request = command.Request;

        var window = listing.SetAvailabilityWindow(
            request.AvailableFrom ?? listing.Availability.AvailableFrom,
            request.AvailableUntil ?? listing.Availability.AvailableUntil,
            request.MinimumStayNights ?? listing.Availability.MinimumStayNights,
            request.MaximumStayNights ?? listing.Availability.MaximumStayNights,
            request.BaseRevision);

        if (window.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(window.Error);
        }

        if (request.TotalUnits is { } units)
        {
            var applied = listing.SetTotalUnits(units, baseRevision: null);

            if (applied.IsFailure)
            {
                return Result.Failure<HousingListingResponse>(applied.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(listing, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }
}
