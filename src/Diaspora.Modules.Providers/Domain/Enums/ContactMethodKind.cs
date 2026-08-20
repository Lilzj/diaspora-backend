namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// A way to reach a provider.
/// <para>
/// The first four are the client's <c>contact</c> object flattened — it carries <c>phone</c>,
/// <c>whatsApp</c>, <c>website</c> and (on organizations) <c>email</c> as four named
/// properties. A list of typed methods rather than four columns because the brief requires
/// social links, and a provider with two phone numbers is ordinary. Four columns would have
/// meant adding a column per network for as long as networks keep being invented.
/// </para>
/// <para>
/// <see cref="WhatsApp"/> is separate from <see cref="Phone"/> deliberately: the client's
/// provider screen offers "Call" and the conversation hub offers WhatsApp, they are frequently
/// different numbers, and collapsing them would make one of the two buttons wrong.
/// </para>
/// </summary>
public enum ContactMethodKind
{
    Phone = 1,
    WhatsApp = 2,
    Email = 3,
    Website = 4,

    /// <summary>An in-platform conversation. Carries no value; the provider id is the address.</summary>
    PlatformMessage = 5,

    Instagram = 10,
    Facebook = 11,
    X = 12,
    LinkedIn = 13,
    TikTok = 14,
    YouTube = 15,
    Telegram = 16,

    /// <summary>Anything else, with a caller-supplied label.</summary>
    Other = 99
}
