using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Diaspora.Modules.Integrations.Extensibility.Http;
using Diaspora.Modules.Integrations.Observability;
using Diaspora.Modules.Integrations.Options;
using Diaspora.Platform.Operations.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Integrations.Infrastructure.Http;

/// <summary>
/// The outbound boundary. Allow-listed, resilient, timed out, and instrumented.
/// <para>
/// Everything this class does is described on <see cref="IIntegrationHttpGateway"/>. What is worth
/// saying here is the ordering, because it is the only ordering that is safe:
/// </para>
/// <list type="number">
/// <item><b>Allow-list first.</b> Before a client is resolved and before the executor is entered, so
/// a refused destination costs nothing and never appears in a breaker's failure ratio. A
/// disallowed host counted as a dependency failure would let a misconfiguration open the circuit for
/// a provider that is perfectly healthy.</item>
/// <item><b>Idempotency gate second.</b> A non-idempotent request is run through the executor with a
/// dependency name that resolves to a no-retry policy — see <see cref="SendAsync"/>. §17.</item>
/// <item><b>Executor third.</b> B17 composes bulkhead, breaker, retry and per-attempt timeout in
/// that fixed order.</item>
/// <item><b>Response classification last</b>, and never as an exception: a 429 is information the
/// adapter needs, not a failure to be swallowed.</item>
/// </list>
/// </summary>
public sealed class IntegrationHttpGateway : IIntegrationHttpGateway
{
    /// <summary>
    /// The named client. Registered through <c>IHttpClientFactory</c>, so connections are pooled and
    /// DNS is refreshed — §40, and the reason an integration does not keep calling a decommissioned
    /// address for the life of a process.
    /// </summary>
    public const string HttpClientName = "diaspora.integrations";

    private readonly IHttpClientFactory _clients;
    private readonly IResilienceExecutor _resilience;
    private readonly IOptionsMonitor<IntegrationsOptions> _options;
    private readonly ILogger<IntegrationHttpGateway> _logger;

    public IntegrationHttpGateway(
        IHttpClientFactory clients,
        IResilienceExecutor resilience,
        IOptionsMonitor<IntegrationsOptions> options,
        ILogger<IntegrationHttpGateway> logger)
    {
        _clients = clients;
        _resilience = resilience;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Whether a URL may be called.
    /// <para>
    /// Four gates, and every one of them has been an incident somewhere:
    /// </para>
    /// <list type="bullet">
    /// <item><b>HTTPS only.</b> A provider token on a plaintext connection is a token on the wire.</item>
    /// <item><b>No credentials in the authority.</b> <c>https://user:pass@host/</c> is a valid URL
    /// and a way to smuggle a secret into a log.</item>
    /// <item><b>No literal address.</b> An IP host cannot be allow-listed meaningfully and is the
    /// shape every metadata-service SSRF takes — <c>169.254.169.254</c>, <c>[::1]</c>,
    /// <c>127.0.0.1</c>. Providers are named, so refusing literals costs nothing.</item>
    /// <item><b>Exact host match against the allow-list.</b> Not a suffix match:
    /// <c>EndsWith("google.com")</c> admits <c>evilgoogle.com</c>. A subdomain is admitted only by a
    /// leading-dot entry, which is an explicit decision rather than an accident of string
    /// comparison.</item>
    /// </list>
    /// </summary>
    public bool IsAllowed(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            || IPAddress.TryParse(uri.Host, out _))
        {
            return false;
        }

        var host = uri.Host.Trim().TrimEnd('.').ToLowerInvariant();

        foreach (var entry in _options.CurrentValue.AllowedOutboundHosts)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var allowed = entry.Trim().TrimEnd('.').ToLowerInvariant();

            if (allowed.StartsWith('.'))
            {
                // A leading dot means "this domain and anything under it", written deliberately.
                if (host.EndsWith(allowed, StringComparison.Ordinal)
                    || string.Equals(host, allowed[1..], StringComparison.Ordinal))
                {
                    return true;
                }

                continue;
            }

            if (string.Equals(host, allowed, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IntegrationHttpResponse> SendAsync(
        IntegrationHttpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAllowed(request.Uri))
        {
            // Deliberately before the executor. See the class summary.
            IntegrationsTelemetry.OutboundBlocked.Add(
                1, new KeyValuePair<string, object?>("dependency", request.Dependency));

            _logger.LogWarning(
                "An outbound call to host '{Host}' for {Dependency} was refused: the host is not on "
                + "this deployment's integration allow-list.",
                request.Uri.Host,
                request.Dependency);

            return new IntegrationHttpResponse
            {
                Reached = false,
                TransportError = "The destination is not on this deployment's outbound allow-list."
            };
        }

        var options = _options.CurrentValue;
        var timeout = request.Timeout ?? TimeSpan.FromSeconds(options.ProviderTimeoutSeconds);

        // A non-idempotent call is executed under a distinct dependency name whose retry policy is
        // "never". Two names rather than a flag, because the decision has to reach B17 — which
        // resolves policy from the name — and because a `.no-retry` suffix in a metric label is how
        // an operator sees that a provider call was deliberately attempted once.
        var dependency = request.IsIdempotent
            ? request.Dependency
            : request.Dependency + ".no-retry";

        var started = TimeProvider.System.GetTimestamp();

        try
        {
            var response = await _resilience.ExecuteAsync(
                dependency,
                async token => await AttemptAsync(request, timeout, options.MaxResponseBytes, token),
                cancellationToken);

            IntegrationsTelemetry.RecordOutbound(
                request.Dependency,
                response.StatusCode,
                TimeProvider.System.GetElapsedTime(started));

            return response;
        }
        catch (CircuitOpenException exception)
        {
            // The dependency was not called, which is a materially different fact from a timeout:
            // nothing happened, so a retry at a higher level is safe in a way it otherwise is not.
            IntegrationsTelemetry.RecordOutboundFailure(request.Dependency, "circuit_open");

            return new IntegrationHttpResponse
            {
                Reached = false,
                TransportError =
                    $"The circuit for this provider is open until {exception.ClosesAt:O}. "
                    + "The provider was not called."
            };
        }
        catch (BulkheadFullException)
        {
            IntegrationsTelemetry.RecordOutboundFailure(request.Dependency, "bulkhead_full");

            return new IntegrationHttpResponse
            {
                Reached = false,
                TransportError = "The platform is at capacity for calls to this provider. "
                    + "The provider was not called."
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            IntegrationsTelemetry.RecordOutboundFailure(request.Dependency, "timeout");

            return new IntegrationHttpResponse
            {
                Reached = false,
                TransportError = "The provider did not answer within the configured timeout."
            };
        }
        catch (HttpRequestException exception)
        {
            IntegrationsTelemetry.RecordOutboundFailure(request.Dependency, "transport");

            _logger.LogWarning(
                exception, "A call to {Dependency} failed at the transport.", request.Dependency);

            return new IntegrationHttpResponse
            {
                Reached = false,
                TransportError = "The provider could not be reached."
            };
        }
        catch (SocketException exception)
        {
            IntegrationsTelemetry.RecordOutboundFailure(request.Dependency, "socket");

            _logger.LogWarning(
                exception, "A call to {Dependency} failed at the socket.", request.Dependency);

            return new IntegrationHttpResponse
            {
                Reached = false,
                TransportError = "The provider could not be reached."
            };
        }
    }

    /// <summary>
    /// One attempt.
    /// <para>
    /// The timeout is applied here rather than on the <c>HttpClient</c>, because a client-level
    /// timeout is per client and this one is per attempt — which is what stops a hung call consuming
    /// the retry budget the executor is managing.
    /// </para>
    /// </summary>
    private async Task<IntegrationHttpResponse> AttemptAsync(
        IntegrationHttpRequest request,
        TimeSpan timeout,
        int maxResponseBytes,
        CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timer.Token);

        using var message = new HttpRequestMessage(request.Method, request.Uri);

        if (!string.IsNullOrWhiteSpace(request.BearerToken))
        {
            message.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.BearerToken);
        }

        if (request.Headers is { Count: > 0 })
        {
            foreach (var (name, value) in request.Headers)
            {
                // Never through this dictionary. The one place a credential may be set is
                // `BearerToken`, so that redaction is structural rather than a filter list.
                if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                message.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (request.Body is not null)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8, request.ContentType);
        }

        var client = _clients.CreateClient(HttpClientName);

        using var response = await client.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, linked.Token);

        var body = await ReadBoundedAsync(response, maxResponseBytes, linked.Token);

        return new IntegrationHttpResponse
        {
            Reached = true,
            StatusCode = (int)response.StatusCode,
            Body = body,
            RetryAfter = ParseRetryAfter(response)
        };
    }

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/>.
    /// <para>
    /// A content-length header is a claim, not a fact, so the cap is enforced while reading rather
    /// than by trusting the header. A response that exceeds it is truncated rather than refused —
    /// the status code is still the useful part, and an adapter parsing a truncated body will fail
    /// to parse, which is the correct outcome.
    /// </para>
    /// </summary>
    private static async Task<string> ReadBoundedAsync(
        HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[8192];
        using var accumulated = new MemoryStream();

        while (accumulated.Length < maxBytes)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            var take = (int)Math.Min(read, maxBytes - accumulated.Length);
            accumulated.Write(buffer, 0, take);
        }

        return Encoding.UTF8.GetString(accumulated.ToArray());
    }

    /// <summary>
    /// The provider's <c>Retry-After</c>, in either form the header permits.
    /// <para>
    /// Honoured rather than guessed at: a platform that invented its own backoff here would be
    /// arguing with a provider about the provider's own quota. A date in the past yields null rather
    /// than a negative delay.
    /// </para>
    /// </summary>
    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter is not { } header)
        {
            return null;
        }

        if (header.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (header.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;

            return remaining > TimeSpan.Zero ? remaining : null;
        }

        return double.TryParse(
            response.Headers.TryGetValues("Retry-After", out var values) ? values.FirstOrDefault() : null,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }
}
