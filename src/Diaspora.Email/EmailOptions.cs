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

    /// <summary>
    /// Where a notification's in-app route becomes a link somebody can click in an inbox.
    /// </summary>
    /// <remarks>
    /// A notification carries a route like <c>/housing/123</c>, which means nothing in a mail
    /// client. Joined to this it becomes a real address. Left empty, messages simply carry no
    /// button — which is the right failure, because a link to nowhere is worse than none.
    /// </remarks>
    public string AppBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Sender identities for particular kinds of mail, keyed by notification category.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A receipt and a security alert should not arrive from the same mailbox. A member who filters
    /// <c>no-reply@</c> into a folder they never read has silently opted out of being told their
    /// account was suspended, and a bank that sends statements from its marketing address teaches
    /// people to ignore the address that matters.
    /// </para>
    /// <para>
    /// Keyed by <c>NotificationCategoryKeys</c> — <c>security</c>, <c>billing</c>, <c>trust</c>,
    /// <c>platform</c> and the rest — so the split is configuration rather than code, and a
    /// deployment that wants one address for everything simply sets none of these. Anything not
    /// listed falls back to <see cref="From"/>.
    /// </para>
    /// <para>
    /// Every address here has to be on a domain the provider has verified. Resend refuses an
    /// unverified <c>from</c>, and its refusal names the message rather than the configuration —
    /// which is why <see cref="EmailOptionsValidator"/> checks these at startup instead.
    /// </para>
    /// </remarks>
    public Dictionary<string, EmailSenderIdentity> Senders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The identity mail in <paramref name="categoryKey"/> is sent from.
    /// </summary>
    /// <remarks>
    /// Falls back to <see cref="From"/> rather than failing, because a category nobody configured
    /// an address for is the normal case: the split exists for the handful of kinds where it
    /// matters, not as an obligation to enumerate every category the platform has.
    /// </remarks>
    public EmailSenderIdentity SenderFor(string? categoryKey) =>
        categoryKey is { Length: > 0 } key
        && Senders.TryGetValue(key, out var sender)
        && sender.IsConfigured
            ? sender
            : From;

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
