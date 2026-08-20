using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diaspora.Push.Firebase;

/// <summary>
/// A Google service-account key, and the OAuth2 access token it can be exchanged for.
///
/// <para>
/// FCM's HTTP v1 API takes a short-lived bearer token rather than the old static server key. The
/// exchange is the standard <c>urn:ietf:params:oauth:grant-type:jwt-bearer</c> flow: sign a JWT
/// with the service account's private key, POST it to the token endpoint, get an access token
/// back. It is implemented here rather than taken from a Google SDK because the whole dependency
/// is one signature and one form post, and pulling a vendor SDK into the build to do it would
/// undo the isolation this project exists for.
/// </para>
///
/// <para>
/// <b>Nothing here is ever logged.</b> The private key, the assertion and the access token are all
/// credentials; the only thing this type will say out loud is whether it currently holds a valid
/// token and when that expires.
/// </para>
/// </summary>
public sealed class ServiceAccountCredential : IDisposable
{
    private readonly ServiceAccountKey _key;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Refresh a minute before the token actually expires. A token that lapses mid-flight comes
    /// back as a 401 that looks exactly like a misconfigured key, and the margin costs nothing.
    /// </summary>
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromSeconds(60);

    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

    public ServiceAccountCredential(ServiceAccountKey key, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(http);

        _key = key;
        _http = http;
    }

    public string ProjectId => _key.ProjectId;

    /// <summary>The service account's address. Safe to log — it identifies, it does not authorise.</summary>
    public string ClientEmail => _key.ClientEmail;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - RenewalMargin)
        {
            return _accessToken;
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the gate: several sends racing a cold start would otherwise each
            // perform their own exchange, and Google rate-limits the token endpoint.
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - RenewalMargin)
            {
                return _accessToken;
            }

            var (token, lifetime) = await ExchangeAsync(cancellationToken);
            _accessToken = token;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(lifetime);
            return token;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<(string Token, int ExpiresInSeconds)> ExchangeAsync(CancellationToken cancellationToken)
    {
        var assertion = CreateSignedAssertion();

        using var request = new HttpRequestMessage(HttpMethod.Post, _key.TokenUri)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", assertion),
            ]),
        };

        using var response = await _http.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The body can echo the assertion, so it is not surfaced. The status and the service
            // account address are enough to tell a wrong key from a wrong clock.
            throw new InvalidOperationException(
                $"Token exchange for {_key.ClientEmail} failed with {(int)response.StatusCode}. "
                + "Check that the service-account key is current and that this host's clock is correct.");
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(payload)
            ?? throw new InvalidOperationException("Token endpoint returned a body that is not a token response.");

        return (token.AccessToken, token.ExpiresIn);
    }

    /// <summary>
    /// The signed JWT. RS256 over <c>{header}.{claims}</c>, base64url throughout, as the flow
    /// requires. <c>iat</c> is backdated by a few seconds so a host whose clock runs slightly fast
    /// does not present a token issued in Google's future.
    /// </summary>
    private string CreateSignedAssertion()
    {
        var now = DateTimeOffset.UtcNow;

        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = _key.ClientEmail,
            scope = MessagingScope,
            aud = _key.TokenUri,
            iat = now.AddSeconds(-10).ToUnixTimeSeconds(),
            exp = now.AddMinutes(55).ToUnixTimeSeconds(),
        }));

        var unsigned = $"{header}.{claims}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_key.PrivateKey);

        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{unsigned}.{Base64Url(signature)}";
    }

    /// <summary>
    /// Releases the refresh gate. The <see cref="HttpClient"/> is not disposed here — it comes
    /// from <c>IHttpClientFactory</c>, which owns its lifetime.
    /// </summary>
    public void Dispose() => _refreshGate.Dispose();

    private static string Base64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}

/// <summary>
/// The fields this adapter needs from a service-account JSON key. Everything else in that file is
/// ignored rather than modelled, so a new field appearing in Google's format is not a breaking
/// change here.
/// </summary>
public sealed record ServiceAccountKey
{
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("client_email")]
    public required string ClientEmail { get; init; }

    [JsonPropertyName("private_key")]
    public required string PrivateKey { get; init; }

    [JsonPropertyName("token_uri")]
    public string TokenUri { get; init; } = "https://oauth2.googleapis.com/token";

    /// <summary>
    /// Reads a key from JSON. Throws with a message naming the missing field rather than a
    /// deserialisation error, because the usual cause is the wrong file — an OAuth *client*
    /// credential rather than a *service account* key looks similar and has neither field.
    /// </summary>
    public static ServiceAccountKey Parse(string json)
    {
        var key = JsonSerializer.Deserialize<ServiceAccountKey>(json)
            ?? throw new InvalidOperationException("The credential file is not a JSON object.");

        if (string.IsNullOrWhiteSpace(key.ProjectId)
            || string.IsNullOrWhiteSpace(key.ClientEmail)
            || string.IsNullOrWhiteSpace(key.PrivateKey))
        {
            throw new InvalidOperationException(
                "The credential file is missing project_id, client_email or private_key. "
                + "A Firebase service-account key has all three; an OAuth client credential has none of them.");
        }

        return key;
    }
}
