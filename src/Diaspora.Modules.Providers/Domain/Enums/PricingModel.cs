namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// How a service is priced.
/// <para>
/// The client models exactly one of these — <c>priceRange: { min, max, currency }</c> — and
/// renders it as <c>£8 — £12</c>. That works for a plate of jollof and is wrong for everything
/// else: a braider charges by the style, an immigration adviser by the hour, a church charges
/// nothing, and a builder quotes. Rendering "£0 — £0" for a free service, which is what the
/// single model forces, is worse than rendering nothing.
/// </para>
/// <para>
/// The model is what decides which amounts are meaningful, and
/// <c>ProviderPricing</c> validates that pairing rather than trusting the caller — a
/// <see cref="Quote"/> with a maximum is a contradiction, and accepting it produces a price
/// that renders differently on every screen that tries.
/// </para>
/// </summary>
public enum PricingModel
{
    /// <summary>Not published. The provider will tell you if you ask.</summary>
    Unspecified = 0,

    /// <summary>One amount. <c>MinAmount</c> carries it; <c>MaxAmount</c> must be null.</summary>
    Fixed = 1,

    /// <summary>A band. Both amounts required, minimum at or below maximum. The client's shape.</summary>
    Range = 2,

    /// <summary>Per hour.</summary>
    Hourly = 3,

    /// <summary>Per day.</summary>
    Daily = 4,

    /// <summary>Per session, appointment or visit.</summary>
    PerSession = 5,

    /// <summary>Per person. Catering, tours, classes.</summary>
    PerPerson = 6,

    /// <summary>Per unit of something the provider names in <c>Unit</c>. Per kilo, per mile.</summary>
    PerUnit = 7,

    /// <summary>Recurring. <c>Unit</c> carries the period.</summary>
    Subscription = 8,

    /// <summary>No published price; the provider quotes on enquiry. Both amounts must be null.</summary>
    Quote = 9,

    /// <summary>Free. Both amounts must be null, and the client renders "Free" rather than "£0".</summary>
    Free = 10,

    /// <summary>Whatever the member chooses to give. A church, a charity, a community kitchen.</summary>
    Donation = 11
}
