using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Payouts;

namespace Diaspora.Modules.Financial.Options;

/// <summary>
/// The payout capability matrix, as a deployment states it.
/// <para>
/// <b>Configuration rather than code, because the two change for different reasons and on different
/// days.</b> A provider adding a country is somebody else's release; a deployment finishing an
/// onboarding call is a Tuesday afternoon. Neither should require this module to be rebuilt, and a
/// matrix compiled into an assembly would guarantee both do.
/// </para>
/// <para>
/// <b>Nothing is configured by default and that is the safe end.</b> An empty matrix routes nothing,
/// so a deployment that has not filled this in refuses payouts with a reason rather than guessing at
/// one. The alternative — a shipped table of what providers are believed to support — would be this
/// module asserting facts about vendors it deliberately knows nothing about.
/// </para>
/// </summary>
public sealed class PayoutRoutingOptions
{
    public const string SectionName = "Financial:PayoutRouting";

    /// <summary>One entry per provider, country, rail and currency. No wildcards — see <see cref="PayoutCapability"/>.</summary>
    public IList<PayoutCapabilityOptions> Capabilities { get; set; } = [];
}

/// <summary>
/// One configured row of the matrix.
/// <para>
/// Mirrors <see cref="PayoutCapability"/> rather than reusing it, because a bound configuration
/// object needs settable properties and a default constructor, and the domain record should not
/// acquire either just to be bindable.
/// </para>
/// </summary>
public sealed class PayoutCapabilityOptions
{
    /// <summary>The adapter name, matching what an <c>ISettlementProvider</c> answers to.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>ISO 4217.</summary>
    public string Currency { get; set; } = string.Empty;

    public PayoutDestinationKind Method { get; set; }

    /// <summary>
    /// Defaults to <see cref="CapabilitySupport.Unknown"/>, so a row somebody added without
    /// confirming the capability is not routed to.
    /// </summary>
    public CapabilitySupport Support { get; set; } = CapabilitySupport.Unknown;

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    public long? MinimumMinorUnits { get; set; }

    public long? MaximumMinorUnits { get; set; }

    public bool RequiresVerifiedPayee { get; set; }

    /// <summary>The provider's own onboarding. See <see cref="PayoutCapability.RequiresProviderOnboarding"/>.</summary>
    public bool RequiresProviderOnboarding { get; set; }

    /// <summary>The documentation reference, or the open question. See <see cref="PayoutCapability.Note"/>.</summary>
    public string? Note { get; set; }
}
