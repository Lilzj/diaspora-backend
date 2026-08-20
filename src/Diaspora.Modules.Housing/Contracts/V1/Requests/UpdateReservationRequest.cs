namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Moves a reservation along.
/// <para>
/// One endpoint for every transition, for the reason the viewing update gives: the client renders
/// one action bar and the aggregate owns which moves are legal.
/// </para>
/// </summary>
public sealed record UpdateReservationRequest
{
    /// <summary>Accept, Decline, LeaseSigned, Occupy, Cancel or Extend.</summary>
    public required string Action { get; init; }

    /// <summary>
    /// A new hold expiry. Accepted with Accept, which is exactly when a landlord says how long they
    /// will keep it, and with Extend.
    /// </summary>
    public DateTimeOffset? HoldExpiresAt { get; init; }

    public string? Reason { get; init; }
}
