namespace Diaspora.Modules.Integrations.Extensibility.Http;

/// <summary>
/// The only way out of this platform to a third party.
/// <para>
/// <b>Four things happen here that must not happen in an adapter, and each one is a rule this
/// platform already carries.</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>The destination is checked against an allow-list before a socket is opened.</b> A provider
/// endpoint arrives from configuration, and configuration is edited by people; a webhook's callback
/// URL arrives from a provider. Either is an SSRF vector into a cloud metadata service or a private
/// subnet, and the check is host-based rather than scheme-based because <c>https://169.254.169.254</c>
/// is a perfectly valid HTTPS URL.
/// </item>
/// <item>
/// <b>The call runs under B17's resilience executor.</b> Retry, backoff, jitter, the circuit
/// breaker and the bulkhead are all the platform's, and B17-R4 fails the build on an adapter that
/// writes its own loop. An adapter calling <c>HttpClient</c> directly would be invisible to the
/// dashboard and uncoordinated with the breaker meant to stop it.
/// </item>
/// <item>
/// <b>Every request has an explicit timeout.</b> §39. A call with no timeout is a thread held until
/// the far end decides otherwise, and the far end is a third party.
/// </item>
/// <item>
/// <b>The client comes from <c>IHttpClientFactory</c>.</b> §40. A per-request <c>HttpClient</c>
/// exhausts sockets under load and pins DNS for the life of the process, which is how an integration
/// keeps calling an IP a provider decommissioned.
/// </item>
/// </list>
/// <para>
/// <b>No token is logged, ever.</b> The gateway sets the <c>Authorization</c> header from
/// <see cref="IntegrationHttpRequest.BearerToken"/> and that property is the only place a token
/// appears; the request is never serialised into telemetry, and the redaction is structural rather
/// than a filter somebody has to maintain.
/// </para>
/// </summary>
public interface IIntegrationHttpGateway
{
    /// <summary>
    /// Makes one call.
    /// <para>
    /// Returns a response rather than throwing on a non-2xx: a provider saying 429 is information,
    /// not an exception, and the adapter above needs the status and the <c>Retry-After</c> to
    /// classify it. Transport failures — a socket reset, a timeout, an open breaker — do surface as
    /// a failed <see cref="IntegrationHttpResponse"/> with no status, because they are the states in
    /// which the provider was never reached.
    /// </para>
    /// </summary>
    Task<IntegrationHttpResponse> SendAsync(
        IntegrationHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a URL may be called at all.
    /// <para>
    /// Public so an adapter can validate a provider-supplied callback URL before storing it, rather
    /// than discovering at call time that it points at a private address.
    /// </para>
    /// </summary>
    bool IsAllowed(Uri uri);
}

/// <summary>One outbound call.</summary>
public sealed record IntegrationHttpRequest
{
    /// <summary>
    /// The dependency name B17 resolves policies under — <c>integrations.calendar.google</c>.
    /// <para>
    /// Per provider rather than one for the module, so a breaker opened by one vendor does not
    /// refuse calls to another. A shared name would mean an outage at a calendar provider stops
    /// identity linking.
    /// </para>
    /// </summary>
    public required string Dependency { get; init; }

    public required HttpMethod Method { get; init; }

    public required Uri Uri { get; init; }

    /// <summary>
    /// Set as <c>Authorization: Bearer</c>. The only property that ever holds a credential, and it
    /// is never read by anything but the header assignment.
    /// </summary>
    public string? BearerToken { get; init; }

    /// <summary>Additional headers. Never a credential — use <see cref="BearerToken"/>.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The body, already serialised. Null for a GET or a DELETE.</summary>
    public string? Body { get; init; }

    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// The per-attempt timeout.
    /// <para>
    /// Per attempt rather than for the whole retry sequence, so a hung call cannot consume the
    /// budget for the retries that would have succeeded. Null takes the configured default.
    /// </para>
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Whether it is safe to run this more than once.
    /// <para>
    /// <b>The gateway refuses to retry when this is false</b>, regardless of what the breaker or the
    /// retry policy would allow. §17: "do not blindly retry a potentially successful request without
    /// understanding whether the external operation is idempotent." A POST that creates a calendar
    /// entry sets this true only when it carries an idempotency key.
    /// </para>
    /// </summary>
    public bool IsIdempotent { get; init; }
}

/// <summary>What came back, or why nothing did.</summary>
public sealed record IntegrationHttpResponse
{
    /// <summary>Whether the provider was reached at all. False for a timeout, a reset, an open breaker.</summary>
    public required bool Reached { get; init; }

    /// <summary>The status code. Null exactly when <see cref="Reached"/> is false.</summary>
    public int? StatusCode { get; init; }

    /// <summary>The body as text. Capped; a provider that returns a gigabyte is a provider defect.</summary>
    public string? Body { get; init; }

    /// <summary>The provider's <c>Retry-After</c>, parsed. Honoured rather than guessed at.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Why the provider was not reached. Never shown to a member.</summary>
    public string? TransportError { get; init; }

    public bool IsSuccess => Reached && StatusCode is >= 200 and < 300;
}
