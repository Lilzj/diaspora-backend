using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Records that somebody has moved in, without going through a reservation.
/// <para>
/// <b>The path for a landlord who let the property offline, and it must not be awkward.</b> Most
/// lettings on any platform close somewhere else — a phone call, a viewing, a handshake — and a
/// platform that could only record the ones it brokered would show a stale market and would publish
/// neighbourhood statistics computed from a fraction of the truth. That is worse than useless: it
/// would tell a member that a neighbourhood lets slowly when it does not.
/// </para>
/// <para>
/// <b>The units go through the guard, exactly as a reservation's would.</b> A landlord marking a
/// bed occupied while somebody else is mid-reservation on the last one is the same race, and
/// routing it through the same port is what makes the counts reconcilable.
/// </para>
/// </summary>
public sealed class MarkHousingOccupiedCommandHandler
    : ICommandHandler<MarkHousingOccupiedCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IHousingUnitReservationGuard _units;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingListingReadContext _read;
    private readonly IClock _clock;

    public MarkHousingOccupiedCommandHandler(
        HousingListingWriteGate gate,
        IHousingUnitReservationGuard units,
        IHousingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        HousingListingReadContext read,
        IClock clock)
    {
        _gate = gate;
        _units = units;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        MarkHousingOccupiedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(loaded.Error);
        }

        var listing = loaded.Value;
        var request = command.Request;
        var now = _clock.UtcNow;
        var units = Math.Max(1, request.Units);

        if (units > listing.Availability.AvailableUnits)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.NoUnitsAvailable);
        }

        // Held then occupied, through the guard. Marking straight to occupied would bypass the one
        // place contention is arbitrated, and a landlord and a member can genuinely be taking the
        // last bed at the same moment.
        var held = await _units.TryHoldAsync(listing.Id, units, now, cancellationToken);

        if (held.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(held.Error);
        }

        var occupied = await _units.OccupyAsync(listing.Id, units, now, cancellationToken);

        if (occupied.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(occupied.Error);
        }

        var occupantId = request.OccupantUserId is { } occupant && occupant != Guid.Empty
            ? UserId.From(occupant)
            : null;

        var recorded = listing.RecordOccupation(units, occupantId, null, now);

        if (recorded.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(recorded.Error);
        }

        await RecordAnalyticsAsync(listing.Id, request, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HousingTelemetry.Occupations.Add(
            1,
            new KeyValuePair<string, object?>("country", listing.Location.CountryCode),
            new KeyValuePair<string, object?>("type", listing.AccommodationTypeKey));

        var response = await _read.ToResponseAsync(listing, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }

    /// <summary>
    /// Records what the property actually let for.
    /// <para>
    /// The figure a neighbourhood market statistic is made of. Where the landlord did not state
    /// one, the listing's asking rent is not substituted: an assumed figure would be indistinguish-
    /// able from a reported one in the median, and a market statistic built partly on guesses is
    /// worse than one built on fewer honest numbers.
    /// </para>
    /// </summary>
    private async Task RecordAnalyticsAsync(
        HousingListingId listingId,
        Contracts.V1.Requests.MarkOccupiedRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var analytics = await _analytics.GetByListingAsync(listingId, cancellationToken);

        if (analytics is null)
        {
            return;
        }

        var period = Enum.TryParse<RentPeriod>(
            request.RentPeriod, ignoreCase: true, out var parsed)
            ? parsed
            : RentPeriod.None;

        analytics.RecordOccupation(now, request.LetAtAmount, request.CurrencyCode, period);
    }
}
