using Diaspora.Modules.Integrations.Extensibility.Providers;

namespace Diaspora.Modules.Integrations.Infrastructure.Providers;

/// <summary>
/// The OAuth provider a deployment has until it configures one.
/// <para>
/// <b>It answers <c>NotConfigured</c> and it is not a stub.</b> PSR-1.2 recorded the rule after
/// three waves each found a seam that was complete, registered, architecture-tested and reachable by
/// nobody — "a stub is worse than an absence". A stub pretends the capability exists; this states,
/// in a form a client can branch on, that it does not.
/// </para>
/// <para>
/// <b>What it must never do is the thing that would be easiest.</b> Every input needed to fabricate
/// a connection is right here: a provider key, a set of scopes, an authenticated member. Return an
/// <c>ExternalAccount</c> with the member's own email, mint a random token, and the connection
/// centre shows <c>CONNECTED</c> — indistinguishable at the wire from a real grant, and the frontend
/// would render a live connection to an account nobody authorized. §49 forbids it and this class is
/// where the temptation actually lives.
/// </para>
/// </summary>
public sealed class NotConfiguredOAuthProvider : IOAuthProvider
{
    /// <summary>Names no vendor, because there is none.</summary>
    public string Key => "none";

    public bool IsConfigured => false;

    /// <summary>
    /// Empty, rather than echoing what was asked for.
    /// <para>
    /// A client reading the granted-scope list of a connection that does not exist must get an empty
    /// list; echoing the request would show a member scopes nobody granted.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> NarrowScopes(IReadOnlyList<string> requested) => [];

    public IntegrationOutcome<Uri> BuildAuthorizationUri(OAuthAuthorizationParameters parameters) =>
        IntegrationOutcome<Uri>.NotConfigured("this authorization");

    public Task<IntegrationOutcome<OAuthGrant>> ExchangeAsync(
        OAuthExchangeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<OAuthGrant>.NotConfigured("this authorization"));

    public Task<IntegrationOutcome<OAuthGrant>> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<OAuthGrant>.NotConfigured("this authorization"));

    /// <summary>
    /// Reports that nothing was revoked, and does not fail.
    /// <para>
    /// The distinction matters at exactly one call site: a disconnect. A member disconnecting an
    /// integration this deployment cannot reach has still disconnected, and the local record is
    /// dropped either way — the <c>false</c> is what tells the client that the provider side was
    /// not touched, rather than pretending it was.
    /// </para>
    /// </summary>
    public Task<IntegrationOutcome<bool>> RevokeAsync(
        OAuthRevocationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<bool>.Success(false));
}

/// <summary>
/// The calendar provider a deployment has until it contracts one.
/// <para>
/// Same rule as its OAuth counterpart, and the same temptation in a sharper form: this class could
/// return a plausible <c>externalEventId</c> and a <c>writtenAt</c>, the platform would record a
/// link, and the member would be told their event is in their calendar. It is not, and nothing
/// downstream could ever discover that.
/// </para>
/// <para>
/// <see cref="Capabilities"/> is empty rather than listing five it cannot serve, so a client reading
/// the capability report gets the same answer as one calling the route.
/// </para>
/// </summary>
public sealed class NotConfiguredCalendarProvider : IExternalCalendarProvider
{
    public string Key => "none";

    public bool IsConfigured => false;

    public IReadOnlyCollection<ExternalCalendarCapability> Capabilities => [];

    public Task<IntegrationOutcome<IReadOnlyList<ExternalCalendar>>> ListCalendarsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<IReadOnlyList<ExternalCalendar>>.NotConfigured("calendars"));

    public Task<IntegrationOutcome<ExternalCalendarEvent>> CreateEventAsync(
        string accessToken,
        string calendarId,
        ExternalCalendarEventDraft draft,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<ExternalCalendarEvent>.NotConfigured("calendar writing"));

    public Task<IntegrationOutcome<ExternalCalendarEvent>> UpdateEventAsync(
        string accessToken,
        string calendarId,
        string externalEventId,
        ExternalCalendarEventDraft draft,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<ExternalCalendarEvent>.NotConfigured("calendar writing"));

    public Task<IntegrationOutcome<IntegrationVoid>> DeleteEventAsync(
        string accessToken,
        string calendarId,
        string externalEventId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<IntegrationVoid>.NotConfigured("calendar writing"));

    public Task<IntegrationOutcome<ExternalCalendarEvent?>> FindEventAsync(
        string accessToken,
        string calendarId,
        string externalEventId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationOutcome<ExternalCalendarEvent?>.NotConfigured("calendar reading"));
}
