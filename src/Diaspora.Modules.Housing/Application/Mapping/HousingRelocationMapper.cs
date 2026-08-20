using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Viewings;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>
/// Turns viewings and reservations into wire responses.
/// <para>
/// The two are mapped together because they are read together: a member's relocation screen shows
/// their viewings and their reservations side by side, and every handler that loads one loads the
/// listing and landlord names for the other.
/// </para>
/// <para>
/// <b>Neither response carries a property address.</b> The address is disclosed on the listing, by
/// the disclosure service, on the strength of the very viewing being rendered here — putting a copy
/// on the viewing would be a second path to the same data with its own rules to get wrong.
/// </para>
/// </summary>
public static class HousingRelocationMapper
{
    public static HousingViewingResponse ToResponse(
        HousingViewing viewing, HousingListing? listing, LandlordProfile? landlord)
    {
        ArgumentNullException.ThrowIfNull(viewing);

        return new HousingViewingResponse
        {
            Id = viewing.Id.Value,
            ListingId = viewing.ListingId.Value,
            ListingTitle = listing?.Title.Value ?? string.Empty,
            ListingSlug = listing?.Slug.Value,
            LandlordId = viewing.LandlordId.Value,
            LandlordName = landlord?.Identity.DisplayName ?? string.Empty,
            SeekerUserId = viewing.SeekerUserId.Value,
            Status = viewing.Status.ToString(),
            PreferredAt = viewing.Window.PreferredAt,
            ConfirmedAt = viewing.Window.ConfirmedAt,
            DurationMinutes = viewing.Window.DurationMinutes,
            IsVirtual = viewing.Window.IsVirtual,
            Place = viewing.Window.Place,
            Message = viewing.Message,
            LandlordFailedToAttend = viewing.LandlordFailedToAttend,
            ClosureReason = viewing.ClosureReason,
            UnlocksAddress = viewing.UnlocksAddress,
            SatisfiesReservationRequirement = viewing.SatisfiesReservationRequirement,
            History = [.. viewing.History.Select(ToHistory)],
            CreatedAt = viewing.CreatedAt
        };
    }

    public static HousingReservationResponse ToResponse(
        HousingReservation reservation, HousingListing? listing, LandlordProfile? landlord)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new HousingReservationResponse
        {
            Id = reservation.Id.Value,
            ListingId = reservation.ListingId.Value,
            ListingTitle = listing?.Title.Value ?? string.Empty,
            ListingSlug = listing?.Slug.Value,
            LandlordId = reservation.LandlordId.Value,
            LandlordName = landlord?.Identity.DisplayName ?? string.Empty,
            SeekerUserId = reservation.SeekerUserId.Value,
            Status = reservation.Status.ToString(),
            Units = reservation.Units,
            Terms = ToTerms(reservation.Terms),
            ViewingId = reservation.ViewingId?.Value,
            Message = reservation.Message,
            ClosureReason = reservation.ClosureReason,
            HoldsUnits = reservation.HoldsUnits,
            AcceptedAt = reservation.AcceptedAt,
            LeaseSignedAt = reservation.LeaseSignedAt,
            OccupiedAt = reservation.OccupiedAt,
            History = [.. reservation.History.Select(ToHistory)],
            CreatedAt = reservation.CreatedAt
        };
    }

    public static ReservationTermsResponse ToTerms(ReservationTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        return new ReservationTermsResponse
        {
            MoveInDate = terms.MoveInDate,
            MoveOutDate = terms.MoveOutDate,
            HoldExpiresAt = terms.HoldExpiresAt,
            AgreedRent = terms.AgreedRent,
            AgreedDeposit = terms.AgreedDeposit,
            CurrencyCode = terms.CurrencyCode,
            RentPeriod = terms.Period == RentPeriod.None ? null : terms.Period.ToString(),
            LeaseType = terms.LeaseType.ToString(),
            Occupants = terms.Occupants,
            StayNights = terms.StayNights
        };
    }

    private static ViewingHistoryEntryResponse ToHistory(ViewingHistoryEntry entry) => new()
    {
        EntryId = entry.EntryId,
        Status = entry.Status.ToString(),
        ActorId = entry.ActorId?.Value,
        Reason = entry.Reason,
        OccurredAt = entry.OccurredAt
    };

    private static ReservationHistoryEntryResponse ToHistory(ReservationHistoryEntry entry) =>
        new()
        {
            EntryId = entry.EntryId,
            Status = entry.Status.ToString(),
            ActorId = entry.ActorId?.Value,
            Reason = entry.Reason,
            OccurredAt = entry.OccurredAt
        };
}
