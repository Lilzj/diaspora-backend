namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Take the money.
/// <para>
/// The amount is optional: omitting it captures everything authorised, which is the common case.
/// Capturing less is legitimate and common enough — a partial fulfilment — that it must be
/// expressible, and capturing more is refused by the aggregate.
/// </para>
/// </summary>
public sealed record CapturePaymentRequest
{
    /// <summary>Major units. Null captures the full authorised amount.</summary>
    public decimal? Amount { get; init; }
}
