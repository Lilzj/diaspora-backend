namespace Diaspora.Modules.Financial.Extensibility.Pricing;

/// <summary>
/// One way of turning a policy into a number.
/// <para>
/// <b>Additive: every registered rule is offered the context and the first that claims it answers.</b>
/// The shipped rules cover percentage commission, flat fees and fixed prices, which is what the
/// catalogue needs today. A deployment wanting tiered commission, volume discounts or seasonal
/// pricing registers a rule; nothing in this module changes and no existing price moves.
/// </para>
/// <para>
/// This is where "smart pricing" lands when B16 arrives — as a rule with a model behind it, offered
/// the same context and answering the same shape.
/// </para>
/// </summary>
public interface IPricingRule
{
    /// <summary>Lower runs first. The shipped rules sit at 100 so an adapter can precede them.</summary>
    int Order { get; }

    /// <summary>Whether this rule has an opinion about this context.</summary>
    bool CanPrice(PricingContext context);

    /// <summary>The figure. Null hands the decision to the next rule.</summary>
    PricingDecision? Price(PricingContext context, IReadOnlyDictionary<string, string> parameters);
}
