namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Suspends a provider, or lifts a suspension. Staff only.
/// <para>
/// <b>The staff route for what the owner's status route used to do for staff (7 Nov 2026).</b>
/// The console suspended a provider through <c>PUT /providers/{id}/status</c>, the same route its
/// owner publishes with, and the reason was optional there. This route takes the decision on its
/// own, with a reason every time.
/// </para>
/// </summary>
public sealed record ModerateProviderRequest
{
    /// <summary><c>Suspend</c> or <c>Reinstate</c>, in any case.</summary>
    public required string Action { get; init; }

    /// <summary>
    /// Why. Required for both actions. A suspension's reason is stored on the provider as its
    /// <c>statusReason</c> and shown to the provider's team; a reinstatement's travels on the
    /// status-changed event.
    /// </summary>
    public string? Reason { get; init; }
}
