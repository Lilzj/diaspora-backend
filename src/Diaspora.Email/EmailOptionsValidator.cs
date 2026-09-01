using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Diaspora.Email;

/// <summary>
/// Refuses an email configuration that would fail at the first send.
/// </summary>
/// <remarks>
/// <para>
/// Validated at startup rather than discovered at dispatch. A misconfigured provider that only
/// fails when the first notification is queued produces a delivery incident hours after the
/// deploy that caused it, attributed to whatever the queue happened to be draining.
/// </para>
/// <para>
/// Every message names the configuration path, because that is the thing the operator has to
/// change and the thing a stack trace will not tell them.
/// </para>
/// </remarks>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DefaultProvider))
        {
            // Not a failure. With one gateway there is nothing to disambiguate, and demanding a
            // name that can only have one value is ceremony. The registration selects the single
            // credentialed provider; this only has to reject a *wrong* name.
        }
        else if (!IsKnown(options.DefaultProvider))
        {
            failures.Add(
                $"'{EmailOptions.SectionName}:DefaultProvider' is '{options.DefaultProvider}', "
                + $"which is not a provider this build carries. Known: {KnownProviders}.");
        }
        else if (!IsCredentialed(options, options.DefaultProvider))
        {
            failures.Add(
                $"'{EmailOptions.SectionName}:DefaultProvider' names {options.DefaultProvider}, "
                + $"but '{EmailOptions.SectionName}:{options.DefaultProvider}' has no credentials.");
        }

        // ── The sender identity ───────────────────────────────────────────────────────────
        //
        // Checked here rather than left to the provider, because Resend's refusal for an
        // unverified or malformed `from` names the message, not the configuration — an operator
        // reading it concludes the notification is wrong.
        if (string.IsNullOrWhiteSpace(options.From.Address))
        {
            failures.Add(
                $"'{EmailOptions.SectionName}:From:Address' is required. Outbound mail has to say "
                + "who it is from, and the address has to be on a domain the provider has verified.");
        }
        else if (!IsParseableAddress(options.From.Address))
        {
            failures.Add(
                $"'{EmailOptions.SectionName}:From:Address' is '{options.From.Address}', "
                + "which is not a valid email address.");
        }

        // ── Every per-category sender, not just the default ───────────────────────────────
        //
        // These are the addresses least likely to be exercised in development and most likely to
        // be wrong: a deployment sends its first receipt weeks after its first welcome. Checking
        // them at startup turns a silent billing-mail outage into a boot failure naming the key.
        foreach (var (categoryKey, sender) in options.Senders)
        {
            // A blank is not an error. Shipped configuration carries the keys with empty values so
            // an operator can see which categories exist, and a deployment that overrides only
            // some of them leaves the rest blank on purpose — `SenderFor` falls back to the
            // default for exactly that case. Refusing blanks here would turn the documentation
            // into a boot failure.
            if (string.IsNullOrWhiteSpace(sender.Address))
            {
                continue;
            }

            if (!IsParseableAddress(sender.Address))
            {
                failures.Add(
                    $"'{EmailOptions.SectionName}:Senders:{categoryKey}:Address' is "
                    + $"'{sender.Address}', which is not a valid email address.");
            }
        }

        if (options.TimeoutSeconds <= 0)
        {
            failures.Add($"'{EmailOptions.SectionName}:TimeoutSeconds' must be at least 1.");
        }

        if (!string.IsNullOrWhiteSpace(options.UnsubscribeBaseUrl)
            && !Uri.TryCreate(options.UnsubscribeBaseUrl, UriKind.Absolute, out _))
        {
            failures.Add(
                $"'{EmailOptions.SectionName}:UnsubscribeBaseUrl' is not an absolute URL. A "
                + "relative List-Unsubscribe header is ignored by every mail client.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private const string KnownProviders = EmailProviderNames.Resend;

    private static bool IsKnown(string provider) =>
        string.Equals(provider, EmailProviderNames.Resend, StringComparison.OrdinalIgnoreCase);

    private static bool IsCredentialed(EmailOptions options, string provider) =>
        string.Equals(provider, EmailProviderNames.Resend, StringComparison.OrdinalIgnoreCase)
        && options.Resend.IsConfigured;

    /// <summary>
    /// Whether the address is one a mail server would accept.
    /// </summary>
    /// <remarks>
    /// <see cref="MailAddress"/> rather than a regular expression: it implements the grammar,
    /// and a hand-rolled pattern here would disagree with the one the provider applies. The only
    /// question being asked is "would this parse", not "does this mailbox exist".
    /// </remarks>
    private static bool IsParseableAddress(string address)
    {
        try
        {
            var parsed = new MailAddress(address);

            return !string.IsNullOrWhiteSpace(parsed.Host) && parsed.Host.Contains('.');
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
