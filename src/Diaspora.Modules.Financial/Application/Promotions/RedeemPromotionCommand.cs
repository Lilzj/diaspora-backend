using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Promotions;

/// <summary>
/// Quotes a coupon: what it would take off, or which refusal it is.
/// <para>
/// <b>It writes nothing, and that is deliberate.</b> A member typing a code into a checkout wants to
/// know what it does before they commit; spending the code at that moment would take it from
/// somebody who then changed their mind. A code is redeemed by the purchase that uses it, which is
/// also the only place the discount can be attributed to something.
/// </para>
/// <para>
/// <b>It is a command rather than a query on purpose.</b> Quoting is the entry point a future
/// credit-bonus redemption grows from — that path does write, and moving the surface later would be
/// a client change. Recorded as B15's <c>PromotionKind.CreditBonus</c>, declared and not yet
/// redeemable standalone.
/// </para>
/// <para>
/// No <c>[Transactional]</c>: the handler saves at most once, and EF wraps a single save in its own
/// transaction. The platform's handbook is explicit that a guarantee asserted in an attribute and
/// needed by nothing is worse than no attribute.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PromotionsRedeem)]
[Auditable(Action = "financial.promotion.redeem")]
public sealed record RedeemPromotionCommand : ICommand<PromotionQuoteResponse>
{
    public required string Code { get; init; }

    /// <summary>The product the code would apply to. Null quotes against the code's own terms.</summary>
    public string? ProductCode { get; init; }

    public string? CountryCode { get; init; }
}
