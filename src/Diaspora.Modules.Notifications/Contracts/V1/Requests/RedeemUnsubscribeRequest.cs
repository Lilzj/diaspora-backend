namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>
/// The token out of an email footer — B13.3.
/// <para>
/// <b>Posted in a body rather than read from the query string, and that is the reason the link is
/// two steps.</b> A token in a URL ends up in the browser's history, in the referrer header sent to
/// every asset the confirmation page loads, and in the access log of every proxy between the two —
/// and this one is a credential that works with no session. The link in the footer opens a page; the
/// page posts the token.
/// </para>
/// <para>
/// It is also why the endpoint is a POST. A mail client prefetching a GET would unsubscribe the
/// member without them ever seeing the message, which is the failure mode that made
/// <c>List-Unsubscribe-Post</c> a specification in the first place.
/// </para>
/// </summary>
public sealed record RedeemUnsubscribeRequest
{
    public required string Token { get; init; }
}
