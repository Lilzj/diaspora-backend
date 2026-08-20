namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Stop a subscription renewing.
/// <para>
/// <b><see cref="Immediately"/> defaults to false and should almost always stay there.</b> Somebody
/// who has paid for the month keeps the month; ending access the instant they click cancel is taking
/// back something that was paid for.
/// </para>
/// </summary>
public sealed record CancelSubscriptionRequest
{
    public string? Reason { get; init; }

    /// <summary>End access now rather than at the end of the paid period. Staff-driven.</summary>
    public bool Immediately { get; init; }
}
