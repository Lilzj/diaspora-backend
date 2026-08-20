namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// What something costs.
/// <para>
/// Carries both the structure and <see cref="DisplayText"/>, which is the pre-formatted string a
/// client renders. Both, deliberately: three clients formatting the same price three ways is the
/// fragmentation this platform already has in its media URLs, and a price shown differently in a
/// list and on a detail screen reads as a bug to the person being charged it. The structure is
/// there for a client that wants a slider or a comparison.
/// </para>
/// </summary>
public sealed record PricingResponse
{
    /// <summary>The <c>PricingModel</c> name — <c>Range</c>, <c>Hourly</c>, <c>Free</c>.</summary>
    public required string Model { get; init; }

    /// <summary>The single amount, or the floor of a range. Null when nothing is published.</summary>
    public decimal? MinAmount { get; init; }

    /// <summary>The ceiling of a range. Null for every other model.</summary>
    public decimal? MaxAmount { get; init; }

    /// <summary>ISO 4217. Null when no amount is published.</summary>
    public string? Currency { get; init; }

    /// <summary>What the amount is per, for <c>PerUnit</c> and <c>Subscription</c>.</summary>
    public string? Unit { get; init; }

    public required bool IsNegotiable { get; init; }

    /// <summary>Rendered server-side so every client shows the same string. May be null.</summary>
    public string? DisplayText { get; init; }
}
