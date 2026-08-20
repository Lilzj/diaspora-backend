namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Check a code, or apply one that grants credit.
/// <para>
/// A code attached to a purchase is applied by the purchase; this is the standalone path — quoting a
/// code before checkout, and redeeming a credit bonus that has nothing to attach to.
/// </para>
/// </summary>
public sealed record RedeemPromotionRequest
{
    public required string Code { get; init; }

    /// <summary>The product the code would be applied to, when quoting one.</summary>
    public string? ProductCode { get; init; }

    public string? CountryCode { get; init; }
}
