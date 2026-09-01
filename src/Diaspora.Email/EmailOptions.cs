using System.ComponentModel.DataAnnotations;

namespace Diaspora.Email;

/// <summary>
/// How this deployment sends email.
/// </summary>
/// <remarks>
/// Shaped after <c>SmsOptions</c> deliberately: an operator who has configured SMS on this
/// platform should recognise every field here without reading anything.
/// </remarks>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Which gateway carries mail. A value of <see cref="EmailProviderNames"/>.
    /// </summary>
    /// <remarks>
    /// Blank means "work it out": if exactly one gateway is credentialed, that one is used. An
    /// explicit name is still better, because it fails loudly when its credentials are missing
    /// rather than silently selecting whichever other provider happens to be configured.
    /// </remarks>
    public string DefaultProvider { get; set; } = string.Empty;

    /// <summary>The transport's last-resort timeout. The per-attempt one is the policy's.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Who the mail is from, when a message does not say.
    /// </summary>
    /// <remarks>
    /// Required to send anything: Resend rejects a <c>from</c> on a domain the account has not
    /// verified, and a default that is wrong fails every send with a message about the sender
    /// rather than about configuration.
    /// </remarks>
    public EmailSenderIdentity From { get; set; } = new();

    /// <summary>
    /// Where a one-click unsubscribe link points.
    /// </summary>
    /// <remarks>
    /// Only used for messages that carry an unsubscribe token. Gmail and Outlook both require a
    /// <c>List-Unsubscribe</c> header of bulk senders, and a platform that sends notification
    /// email without one accumulates a deliverability problem that is far harder to undo than to
    /// avoid.
    /// </remarks>
    public string UnsubscribeBaseUrl { get; set; } = "https://diaspora.app/unsubscribe";

    public ResendOptions Resend { get; set; } = new();
}

/// <summary>The name and address outbound mail is sent from.</summary>
public sealed class EmailSenderIdentity
{
    public string Address { get; set; } = string.Empty;

    public string Name { get; set; } = "Diaspora";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Address);
}

public static class EmailProviderNames
{
    public const string Resend = "Resend";
}

/// <summary>
/// Credentials for Resend's HTTP API.
/// </summary>
/// <remarks>
/// <para>
/// Resend was chosen because it is what the sibling EduTech platform already runs, so the
/// operational knowledge — domain verification, the dashboard, the bounce handling — transfers
/// rather than being learned twice.
/// </para>
/// <para>
/// <b>The API key is a secret</b> and belongs in the same store as
/// <c>Identity__Jwt__SigningKey</c> and the SMS credentials, never in
/// <c>appsettings.json</c>.
/// </para>
/// </remarks>
public sealed class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Overridable so a test can point at a stub rather than the internet.</summary>
    public string BaseUrl { get; set; } = "https://api.resend.com/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
