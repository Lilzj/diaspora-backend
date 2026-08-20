namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// What a redeemed unsubscribe link switched off — B13.3.
/// <para>
/// Carries a category and a route and nothing about the member. The caller holding the link is not
/// necessarily the person it was sent to — a forwarded email, a shared mailbox, a scanner following
/// URLs — so a response naming an address or a member would turn a link into a disclosure.
/// </para>
/// </summary>
public sealed record UnsubscribeResultResponse
{
    public required string CategoryKey { get; init; }

    /// <summary>The human-readable label, so a confirmation page can say what stopped.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Where the rest of the settings live.
    /// <para>
    /// Returned because this page is reached with no session and often on a device that is not the
    /// member's phone. Without it the page can only say "done", and somebody who wanted to change
    /// something else has no route except finding another email with another link in it.
    /// </para>
    /// </summary>
    public required string ManageRoute { get; init; }
}
