using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Brings an archived or deleted property back, as a draft.
/// <para>
/// <b>Loads with the soft-delete filter off, and that is the whole trick.</b> A restore has to read
/// a row the global query filter is hiding; written against the ordinary getter it finds nothing
/// and reports the listing missing, which is a defect the platform has recorded before and which
/// looks exactly like the feature not working.
/// </para>
/// <para>
/// It comes back as a draft rather than as whatever it was. A property that has been off the market
/// wants its rent, its dates and its photographs looked at before anybody is shown it again, and
/// routing the restore through draft is what forces that.
/// </para>
/// </summary>
public sealed class RestoreHousingListingCommandHandler
    : ICommandHandler<RestoreHousingListingCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingListingReadContext _read;
    private readonly IClock _clock;

    public RestoreHousingListingCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingListingReadContext read,
        IClock clock)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        RestoreHousingListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Ignoring the filter. See the class summary.
        var loaded = await _gate.LoadIncludingDeletedAsync(
            command.ListingId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(loaded.Error);
        }

        var listing = loaded.Value;

        if (listing.IsDeleted)
        {
            listing.Restore();
        }
        else if (listing.Status == HousingListingStatus.Archived)
        {
            var restored = listing.RestoreFromArchive(_clock.UtcNow, _authorization.CallerId);

            if (restored.IsFailure)
            {
                return Result.Failure<HousingListingResponse>(restored.Error);
            }
        }
        else
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.ListingNotArchived);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(listing, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }
}
