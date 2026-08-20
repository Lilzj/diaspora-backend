using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Pricing;

/// <summary>
/// Everything a pricing rule is allowed to know.
/// <para>
/// <b>The list is short on purpose.</b> A rule that could reach further would be a rule that
/// couples pricing to a vertical, and the next module's pricing would need a new field on this
/// record. Everything here is either the request or something this module already owns.
/// </para>
/// </summary>
public sealed record PricingContext
{
    public required FinancialPolicyKind PolicyKind { get; init; }

    /// <summary>The figure being priced from — a sale amount for commission, a list price otherwise.</summary>
    public required Money BaseAmount { get; init; }

    public string? PolicyKey { get; init; }

    public string? ProductCode { get; init; }

    public string? ModuleName { get; init; }

    public string? CountryCode { get; init; }

    public MonetizationSubjectType? SubjectType { get; init; }

    public Guid? BuyerUserId { get; init; }

    /// <summary>The subscription tier the buyer holds, when they hold one.</summary>
    public SubscriptionTier? BuyerTier { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}
