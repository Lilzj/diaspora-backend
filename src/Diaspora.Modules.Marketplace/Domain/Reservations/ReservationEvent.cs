using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Reservations;

/// <summary>
/// One step in a reservation's life, recorded as it happened.
/// <para>
/// <b>The brief asks to "track reservation history", and this is it.</b> A reservation that ended
/// as Cancelled tells a buyer nothing; a trail of Pending → AwaitingCollection → Cancelled with
/// the seller named on the last step tells them who called it off and when — which is the fact a
/// cancellation-rate statistic is computed from and the fact a dispute turns on.
/// </para>
/// </summary>
public sealed class ReservationEvent
{
    private ReservationEvent(
        Guid eventId,
        ReservationStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        EventId = eventId;
        Status = status;
        ActorId = actorId;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    private ReservationEvent() { }

    public Guid EventId { get; private set; }

    /// <summary>The state the reservation entered.</summary>
    public ReservationStatus Status { get; private set; }

    /// <summary>Who moved it. Null on an expiry, which the sweep recorded.</summary>
    public UserId? ActorId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static ReservationEvent Create(
        Guid eventId,
        ReservationStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxReservationNote })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxReservationNote];
        }

        return new ReservationEvent(eventId, status, actorId, trimmed, occurredAt);
    }
}
