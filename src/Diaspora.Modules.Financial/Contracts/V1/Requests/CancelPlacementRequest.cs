namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Stop a purchased placement.
/// <para>
/// Whether the money comes back is decided by the refund-window policy, not by this request — which
/// is why there is no "refund" flag here. An operator who wants to refund outside the window issues
/// one against the payment explicitly, and it is recorded as a goodwill refund.
/// </para>
/// </summary>
public sealed record CancelPlacementRequest
{
    public string? Reason { get; init; }
}
