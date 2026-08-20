using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Domain.Reservations;

/// <summary>
/// One step in a reservation's life, recorded as it happened.
/// <para>
/// <b>Append-only, and the record that matters most in this module.</b> A reservation is the point
/// at which a member commits to a home and, in the real world, starts moving money around outside
/// the platform. When something goes wrong, the sequence of who agreed what and when is the whole
/// of what Housing can offer either party, and a trail that could be edited would be worth
/// nothing.
/// </para>
/// <para>
/// The key is supplied by the caller - see ValueGeneratedNever in the configuration, and B10's
/// note about why the symptom of forgetting it is a bogus concurrency conflict.
/// </para>
/// </summary>
public sealed class ReservationHistoryEntry
{
    private ReservationHistoryEntry(
        Guid entryId,
        HousingReservationStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        EntryId = entryId;
        Status = status;
        ActorId = actorId;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    private ReservationHistoryEntry() { }

    public Guid EntryId { get; private set; }

    public HousingReservationStatus Status { get; private set; }

    /// <summary>Who did it. Null when the expiry sweep did.</summary>
    public UserId? ActorId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static ReservationHistoryEntry Create(
        Guid entryId,
        HousingReservationStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmed is { Length: > HousingLimits.MaxReservationNote })
        {
            trimmed = trimmed[..HousingLimits.MaxReservationNote];
        }

        return new ReservationHistoryEntry(entryId, status, actorId, trimmed, occurredAt);
    }
}
