using System.ComponentModel.DataAnnotations;

namespace Diaspora.Sms;

/// <summary>
/// How this deployment sends an SMS, and which wire it uses for which destination.
/// <para>
/// The routing table is the reason this options class exists at all. A single global provider
/// is a configuration value; two providers chosen by destination is a policy, and a policy
/// belongs somewhere an operator can read it.
/// </para>
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// The provider used for any destination no route claims.
    /// <para>
    /// Empty means "no SMS provider is configured", which is the state this platform shipped in
    /// and is a legitimate one for a development host. It is not silently tolerated: see
    /// <see cref="SmsOptionsValidator"/>, which refuses to start a deployed host that has an SMS
    /// section but no working provider, and <see cref="RoutedSmsOtpDeliveryService"/>, which
    /// answers <c>false</c> rather than pretending.
    /// </para>
    /// </summary>
    public string DefaultProvider { get; set; } = string.Empty;

    /// <summary>
    /// Destination prefixes routed away from <see cref="DefaultProvider"/>.
    /// <para>
    /// Deliberately empty by default. A shipped country list is a claim about a provider's
    /// coverage that this repository cannot verify and that changes without warning; a wrong
    /// entry routes a member's sign-in code to a gateway that will not carry it. The
    /// <c>appsettings</c> template carries a filled-in example to copy, so the deployment that
    /// owns the provider account is the one asserting what that account covers.
    /// </para>
    /// </summary>
    public IList<SmsRouteOptions> Routes { get; set; } = [];

    /// <summary>
    /// Whether a destination whose routed provider is failing may be sent through
    /// <see cref="DefaultProvider"/> instead.
    /// <para>
    /// On by default, and only ever applied to a <em>transient</em> failure — a timeout, a 503,
    /// an open circuit. A number the routed provider rejected as invalid is invalid on the
    /// second wire too, and paying a second provider to confirm it is waste.
    /// </para>
    /// </summary>
    public bool FallBackToDefaultProvider { get; set; } = true;

    /// <summary>Per-attempt ceiling. The retry budget is the executor's, not this.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    public TwilioOptions Twilio { get; set; } = new();

    public AfricasTalkingOptions AfricasTalking { get; set; } = new();
}

/// <summary>One routing rule: these destinations go to this provider.</summary>
public sealed class SmsRouteOptions
{
    /// <summary>A value of <see cref="SmsProviderNames"/>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// E.164 prefixes, with or without the leading <c>+</c>.
    /// <para>
    /// Matched longest-first, so <c>+1876</c> (Jamaica) can be routed away from <c>+1</c>
    /// without reordering the file. Prefixes rather than ISO country codes because the thing
    /// being matched is a phone number, and turning a number into a country needs a library
    /// whose data would then be a second thing to keep current.
    /// </para>
    /// </summary>
    public IList<string> CallingCodes { get; set; } = [];
}

/// <summary>The provider identifiers <c>Sms:Routes</c> and <c>Sms:DefaultProvider</c> accept.</summary>
public static class SmsProviderNames
{
    public const string Twilio = "Twilio";

    public const string AfricasTalking = "AfricasTalking";
}

/// <summary>Twilio's Programmable Messaging credentials.</summary>
public sealed class TwilioOptions
{
    public string AccountSid { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// The sending number in E.164, or a Messaging Service SID beginning <c>MG</c>.
    /// <para>
    /// A Messaging Service is the better answer for a global app: it holds a pool of numbers and
    /// picks a sender appropriate to the destination, which is the difference between a UK
    /// member seeing a local number and seeing an American one they will not trust.
    /// </para>
    /// </summary>
    public string From { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !string.IsNullOrWhiteSpace(From);
}

/// <summary>Africa's Talking credentials.</summary>
public sealed class AfricasTalkingOptions
{
    public string Username { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The registered alphanumeric sender id or short code. Optional: without one the account's
    /// default is used, which on a sandbox account is the only thing that works.
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// Uses the sandbox host instead of the live one.
    /// <para>
    /// A separate flag rather than a base-URL override, because the sandbox also only accepts
    /// the literal username <c>sandbox</c>, and an operator who sets one without the other gets
    /// an authentication failure that reads like a bad key.
    /// </para>
    /// </summary>
    public bool UseSandbox { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(ApiKey);
}
