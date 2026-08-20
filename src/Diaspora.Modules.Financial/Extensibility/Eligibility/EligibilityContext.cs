using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Eligibility;

/// <summary>
/// Everything an eligibility rule is allowed to know about a would-be buyer.
/// <para>
/// <b>Facts the caller already holds, never a question asked of another module.</b> The access tier
/// and the verification flag come from the request's own claims; the country comes from the request.
/// A rule that needed to ask Trust or Identity a question would be a synchronous cross-module call
/// on the catalogue path, which is the thing this platform's module graph exists to prevent.
/// </para>
/// </summary>
public sealed record EligibilityContext
{
    public required MonetizationProduct Product { get; init; }

    public required Guid BuyerUserId { get; init; }

    public SubjectReference? Subject { get; init; }

    /// <summary>The caller's access tier, from their own token.</summary>
    public string? AccessTier { get; init; }

    public bool IsVerified { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>The plan code the buyer holds, when they hold one.</summary>
    public string? SubscriptionPlanCode { get; init; }

    /// <summary>Whether this buyer has already bought this product for this subject.</summary>
    public bool HasPurchasedBefore { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}
