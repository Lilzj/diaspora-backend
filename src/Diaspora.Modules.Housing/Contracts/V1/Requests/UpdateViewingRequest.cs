namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Moves a viewing along.
/// <para>
/// One endpoint for every transition rather than five, because the client renders one action bar
/// and the legal moves depend on where the viewing already is — which the aggregate knows and the
/// client should not have to.
/// </para>
/// </summary>
public sealed record UpdateViewingRequest
{
    /// <summary>Schedule, Complete, Cancel or NoShow.</summary>
    public required string Action { get; init; }

    /// <summary>The agreed time. Required when scheduling.</summary>
    public DateTimeOffset? ScheduledFor { get; init; }

    /// <summary>Where to meet, or how to join. Never the property address.</summary>
    public string? Place { get; init; }

    /// <summary>
    /// On a no-show, which party failed to appear. Required rather than inferred: one is a wasted
    /// afternoon and the other is a fraud signal.
    /// </summary>
    public bool? LandlordFailedToAttend { get; init; }

    public string? Reason { get; init; }
}
