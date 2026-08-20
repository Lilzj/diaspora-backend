namespace Diaspora.Modules.Integrations.Contracts.V1.Requests;

/// <summary>
/// Opens an authorization.
/// <para>
/// <b>Not in the frontend's register, and it is not a new contract to negotiate — it is the half of
/// GAP-002 the register describes without naming a route.</b> The register requires that "the
/// redirect URI must be validated server-side against an allow-list" and that <c>state</c> is never
/// trusted without validation. Both are impossible if the client mints the state and composes the
/// URL itself, because there is then nothing for the server to compare against. This route is where
/// the server issues both.
/// </para>
/// <para>
/// The client still performs PKCE and still holds the verifier: it sends the <em>challenge</em> here
/// and the verifier at exchange, which is exactly the split RFC 7636 specifies and keeps the secret
/// on the device.
/// </para>
/// </summary>
public sealed record StartAuthorizationRequest
{
    /// <summary>Where the provider should return the member. Checked against the allow-list.</summary>
    public string? RedirectUri { get; init; }

    /// <summary>Base64url SHA-256 of the client's verifier. Required; <c>plain</c> is not accepted.</summary>
    public string? CodeChallenge { get; init; }

    /// <summary>What the capability needs. Narrowed by the adapter; never widened.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary><c>link</c>, <c>reauthorize</c> or <c>signIn</c>.</summary>
    public string? Intent { get; init; }
}

/// <summary>
/// Completes an authorization.
/// <para>
/// Field-for-field the client's <c>exchangeAuthorizationCode</c> body. <c>state</c> is additive and
/// required: the client already holds it, because this platform issued it at
/// <c>/oauth/{provider}/authorize</c>, and without it there is nothing to look the attempt up by.
/// </para>
/// </summary>
public sealed record ExchangeAuthorizationRequest
{
    public string? Code { get; init; }

    public string? CodeVerifier { get; init; }

    /// <summary>
    /// The state this platform issued.
    /// <para>
    /// Looked up as a hash. A callback carrying a state this platform never issued matches no row
    /// and is refused before a provider call is spent on it.
    /// </para>
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Sent by the client and <b>compared, never used</b>.
    /// <para>
    /// The URI the exchange actually presents to the provider is the one recorded at authorization
    /// time. Accepting the client's would be the canonical code-interception vulnerability, and
    /// comparing rather than ignoring means a mismatch is a visible refusal rather than a silent
    /// substitution.
    /// </para>
    /// </summary>
    public string? RedirectUri { get; init; }

    public IReadOnlyList<string>? Scopes { get; init; }

    public string? Intent { get; init; }
}

/// <summary>Revokes a grant at the provider without disconnecting locally.</summary>
public sealed record RevokeAuthorizationRequest
{
    public Guid? ConnectionId { get; init; }
}

/// <summary>
/// An entry on its way into a calendar.
/// <para>
/// Field-for-field the client's <c>CalendarEventDraft</c> plus <c>calendarId</c>, which the client
/// already spreads into the same body.
/// </para>
/// <para>
/// <b>There is no attendees field.</b> Not optional, not ignored — absent. "Exporting an event must
/// not disclose who else is going to a third party the other attendees never agreed to", and a field
/// that does not exist cannot be populated by a later change that did not read this.
/// </para>
/// </summary>
public sealed record CalendarEventRequest
{
    public string? CalendarId { get; init; }

    /// <summary>Diaspora's own identifier for the thing exported. The deduplication key.</summary>
    public string? SourceId { get; init; }

    /// <summary><c>event</c>, <c>occurrence</c>, <c>series</c> or <c>reminder</c>.</summary>
    public string? SourceKind { get; init; }

    public string? Title { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>
    /// IANA. <b>Required, and never defaulted.</b>
    /// <para>
    /// "A community meeting in Lagos is at 18:00 WAT whether or not the member is currently in
    /// London." A server that substituted its own zone would write a different — and wrong — claim
    /// about a real-world event, and would do it silently.
    /// </para>
    /// </summary>
    public string? TimeZone { get; init; }

    public bool IsAllDay { get; init; }

    public string? Location { get; init; }

    public string? Notes { get; init; }

    public string? Url { get; init; }

    /// <summary>Minutes before the start. Empty means "use the calendar's default".</summary>
    public IReadOnlyList<int>? ReminderMinutes { get; init; }

    /// <summary>RFC 5545 RRULE.</summary>
    public string? RecurrenceRule { get; init; }
}

/// <summary>Starts a synchronisation.</summary>
public sealed record StartCalendarSyncRequest
{
    public string? CalendarId { get; init; }

    /// <summary><c>export</c>, <c>import</c> or <c>both</c>.</summary>
    public string? Direction { get; init; }
}

/// <summary>
/// A partial settings update.
/// <para>
/// Every property is nullable and null means "leave it alone". The client's contract is
/// <c>PUT</c> with <c>Partial&lt;CalendarSyncSettings&gt;</c>, so treating a missing field as an
/// instruction to clear would mean a client sending only <c>enabled</c> wipes the member's default
/// calendar.
/// </para>
/// </summary>
public sealed record UpdateCalendarSettingsRequest
{
    public bool? Enabled { get; init; }

    public string? DefaultCalendarId { get; init; }

    public string? DefaultProvider { get; init; }

    public bool? AutoExportRsvps { get; init; }

    public bool? TwoWaySyncEnabled { get; init; }

    public string? SyncFrequency { get; init; }

    public IReadOnlyList<int>? DefaultReminderMinutes { get; init; }

    public string? TimeZoneBehaviour { get; init; }

    public string? ConflictBehaviour { get; init; }
}
