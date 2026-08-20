namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Asks to see a property.
/// <para>
/// <b>The request that unlocks an address once it is confirmed.</b> Booking a viewing is the
/// intended path to a street name rather than a workaround for one, which is why it is gated on a
/// verified identity.
/// </para>
/// </summary>
public sealed record RequestViewingRequest
{
    /// <summary>When the member would like to come. Null leaves it to the landlord.</summary>
    public DateTimeOffset? PreferredAt { get; init; }

    /// <summary>
    /// A video walk-through rather than a visit. Not a nicety: most members choosing a home are
    /// doing it from another country, and this is the only viewing they can have before they fly.
    /// </summary>
    public bool IsVirtual { get; init; }

    public int? DurationMinutes { get; init; }

    /// <summary>What the member wants to say to the landlord.</summary>
    public string? Message { get; init; }
}
