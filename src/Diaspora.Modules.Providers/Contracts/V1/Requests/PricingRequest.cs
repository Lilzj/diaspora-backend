namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// What something costs.
/// <para>
/// The amounts are validated against the model rather than accepted as sent: a
/// <c>Quote</c> with a maximum is a contradiction, and a <c>Range</c> whose ceiling is below its
/// floor renders as "£12 — £8" on every screen that tries. The client has neither check today.
/// </para>
/// </summary>
public sealed record PricingRequest
{
    /// <summary>The <c>PricingModel</c> name. Defaults to <c>Unspecified</c>.</summary>
    public string? Model { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    /// <summary>ISO 4217. Required whenever an amount is published.</summary>
    public string? Currency { get; init; }

    /// <summary>What the amount is per, for <c>PerUnit</c> and <c>Subscription</c>.</summary>
    public string? Unit { get; init; }

    public bool IsNegotiable { get; init; }
}
