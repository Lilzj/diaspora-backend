namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// Messages that reach a member's mailbox even though email is switched off.
/// <para>
/// <b>Why this exists (7 Oct 2026).</b> Email starts off for every new member, on purpose: nobody
/// who has just signed up asked for mail. But the result of a verification is the answer to a
/// request the member made themselves, often after days of waiting, and an approval that only
/// reached the inbox of an app the member was locked out of was not an answer. The owner decided
/// verification results go by email.
/// </para>
/// <para>
/// <b>Narrow on purpose.</b> This passes only the general email switch. A member who muted the
/// Verification category, or turned on Do Not Disturb, made a specific choice and is still
/// respected; quiet hours still defer. Revocation is not listed because it is Critical, which
/// already overrides every switch.
/// </para>
/// </summary>
public static class AccountMail
{
    private static readonly HashSet<string> RuleKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "trust.verification.approved",
        "trust.verification.rejected",
        "trust.verification.expired",
        // A reviewer asking for more (7 Nov 2026): without it the verification waits forever.
        "trust.verification.more-information",
        // Content put back after a moderation decision (7 Nov 2026). The takedown was Critical and
        // reached email; the owner hears it was undone the same way.
        "moderation.case.restored",
    };

    /// <summary>Whether a rule's email ignores the member's general email switch.</summary>
    public static bool Covers(string ruleKey) => RuleKeys.Contains(ruleKey);
}
