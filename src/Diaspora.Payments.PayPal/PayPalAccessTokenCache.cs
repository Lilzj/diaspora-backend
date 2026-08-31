using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.PayPal;

/// <summary>
/// Holds PayPal's access token and fetches a new one when it is spent.
/// <para>
/// <b>PayPal authenticates a credential pair for a token rather than accepting a key per
/// request.</b> Fetching one per payout would double every call and hit the token endpoint's own
/// rate limit under any real volume, so it is cached — and because the cache is shared, refreshing
/// it has to be single-flight. Without the lock, twenty concurrent payouts on an expired token
/// produce twenty token requests, nineteen of which are wasted and some of which PayPal will
/// throttle.
/// </para>
/// <para>
/// <b>The token is never logged, and neither is the credential that produced it.</b> An access
/// token is a bearer credential for the whole account; a log line carrying one is a log line that
/// can move money.
/// </para>
/// </summary>
public sealed class PayPalAccessTokenCache : IDisposable
{
    public const string HttpClientName = "payments.paypal.token";

    private readonly IOptionsMonitor<PayPalOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<PayPalAccessTokenCache> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;
    private string? _issuedFor;

    public PayPalAccessTokenCache(
        IOptionsMonitor<PayPalOptions> options,
        HttpClient http,
        ILogger<PayPalAccessTokenCache> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// A usable token, or null when one could not be obtained.
    /// <para>
    /// Null rather than an exception: a caller that cannot authenticate has an <em>unknown</em>
    /// payout outcome to report, not a failure to propagate, and turning this into a throw would
    /// make that harder to get right at every call site.
    /// </para>
    /// </summary>
    public async Task<string?> GetAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var settings = _options.CurrentValue;

        if (Usable(settings, now) is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Checked again inside the lock: while this call was queued, whoever held the lock may
            // already have fetched exactly the token being waited for.
            if (Usable(settings, now) is { } fetchedMeanwhile)
            {
                return fetchedMeanwhile;
            }

            return await FetchAsync(settings, now, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Discards the cached token. Called when PayPal rejects it, so the next call refetches.</summary>
    public void Invalidate()
    {
        _token = null;
        _expiresAt = default;
    }

    private string? Usable(PayPalOptions settings, DateTimeOffset now)
    {
        // A credential change must not be served from a token minted for the old one, and a host
        // change must not either — the same token is meaningless against the other environment.
        return _token is not null
            && string.Equals(_issuedFor, Fingerprint(settings), StringComparison.Ordinal)
            && now < _expiresAt
                ? _token
                : null;
    }

    private async Task<string?> FetchAsync(
        PayPalOptions settings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.ApiBase.TrimEnd('/')}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(
                [new KeyValuePair<string, string>("grant_type", "client_credentials")])
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{settings.ClientId}:{settings.ClientSecret}")));

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "PayPal's token endpoint could not be reached.");

            return null;
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The body is not logged: a token error can echo the client id back.
                _logger.LogError(
                    "PayPal refused the configured credentials with {Status}. No payout can be made "
                    + "until '{Section}' is corrected.",
                    (int)response.StatusCode,
                    PayPalOptions.SectionName);

                return null;
            }

            return Store(payload, settings, now);
        }
    }

    private string? Store(string payload, PayPalOptions settings, DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("access_token", out var token)
                || token.GetString() is not { Length: > 0 } value)
            {
                return null;
            }

            var lifetime = document.RootElement.TryGetProperty("expires_in", out var expires)
                && expires.ValueKind == JsonValueKind.Number
                    ? expires.GetInt32()
                    : 0;

            var skew = Math.Max(0, settings.TokenRefreshSkewSeconds);

            _token = value;
            _issuedFor = Fingerprint(settings);

            // A token with no stated lifetime is used once and refetched. Assuming an hour for
            // something PayPal did not promise is how a payout fails on a 401 an hour from now.
            _expiresAt = lifetime > skew ? now.AddSeconds(lifetime - skew) : now;

            return value;
        }
        catch (JsonException)
        {
            _logger.LogError("PayPal's token response could not be parsed.");

            return null;
        }
    }

    /// <summary>
    /// Identifies the credential and host a cached token was minted for, without holding either.
    /// A hash rather than the values, so nothing here can leak a secret into a heap dump.
    /// </summary>
    private static string Fingerprint(PayPalOptions settings) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes($"{settings.ApiBase}\n{settings.ClientId}\n{settings.ClientSecret}")));

    public void Dispose() => _gate.Dispose();
}
