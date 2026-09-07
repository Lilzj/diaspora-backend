using System.Globalization;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Stripe Connect onboarding, hosted by Stripe.
/// <para>
/// <b>Two calls, and the order matters.</b> An Express account is opened first and exists from that
/// moment whether or not the member finishes; an Account Link is then minted against it, and that
/// link is what the member is sent to. Links are single-use and expire in minutes, so the account
/// reference — not the link — is what gets stored.
/// </para>
/// <para>
/// <b>No credential reaches this platform.</b> The member types their bank details into Stripe's
/// pages. What comes back here is <c>acct_…</c>, which is exactly what
/// <see cref="StripeConnectVerificationSource"/> later asks Stripe about and what
/// <see cref="StripeSettlementProvider"/> transfers to.
/// </para>
/// <para>
/// <b>`transfers` is the capability requested, not `card_payments`.</b> The platform sends money to
/// this account; it never charges through it. Requesting more would put the member through
/// compliance questions for something the platform will never do.
/// </para>
/// </summary>
public sealed class StripePayoutOnboarding : IPayoutOnboarding
{
    public const string HttpClientName = "payments.stripe.onboarding";

    private readonly IOptionsMonitor<StripeOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<StripePayoutOnboarding> _logger;

    public StripePayoutOnboarding(
        IOptionsMonitor<StripeOptions> options,
        HttpClient http,
        ILogger<StripePayoutOnboarding> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<PayoutOnboardingSession> StartAsync(
        PayoutOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            return PayoutOnboardingSession.Refused(
                "stripe.not_configured",
                "Payouts are not set up on this deployment yet.");
        }

        var account = request.ExistingAccountReference;

        if (string.IsNullOrWhiteSpace(account))
        {
            var opened = await OpenAccountAsync(request, settings, cancellationToken);

            if (opened.Reference is null)
            {
                return PayoutOnboardingSession.Refused(opened.Code!, opened.Message!);
            }

            account = opened.Reference;
        }

        var link = await MintLinkAsync(account!, request, settings, cancellationToken);

        return link.Url is null

            // The account reference travels with the refusal. It exists at Stripe now, and losing
            // it here would strand it — the next attempt would open a second one for the same
            // person, which is a support conversation nobody can unpick from this side.
            ? PayoutOnboardingSession.Refused(link.Code!, link.Message!, account)
            : PayoutOnboardingSession.Ready(link.Url, account!);
    }

    // ── Stripe ──────────────────────────────────────────────────────────────────────────────

    private async Task<(string? Reference, string? Code, string? Message)> OpenAccountAsync(
        PayoutOnboardingRequest request, StripeOptions settings, CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            // Express: Stripe hosts the onboarding and the account's own dashboard. Custom would
            // make this platform responsible for collecting and re-collecting compliance data.
            new("type", "express"),
            new("country", request.CountryCode.Trim().ToUpperInvariant()),

            // Stripe takes lower-case ISO 4217 and rejects the upper-case Money stores.
            new("default_currency", request.Currency.Trim().ToLowerInvariant()),
            new("capabilities[transfers][requested]", "true"),

            // The tie back, so a Stripe-side investigation can reach the member without this
            // platform having to be asked.
            new("metadata[diaspora_user]", request.PayeeUserId.ToString("N", CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            form.Add(new KeyValuePair<string, string>("email", request.Email));
        }

        var (payload, ok, transport) = await SendAsync(
            "/v1/accounts",
            form,

            // One account per member, whatever happens to the response. A retry after a timeout must
            // not open a second one, and the key is derived from the member rather than the clock.
            $"diaspora-connect-{request.PayeeUserId:N}",
            settings,
            cancellationToken);

        if (transport is not null)
        {
            return (null, transport.Value.Code, transport.Value.Message);
        }

        if (!ok)
        {
            var message = ReadNested(payload!, "error", "message");

            _logger.LogWarning(
                "Stripe refused to open a connected account for {PayeeUserId}: {Message}",
                request.PayeeUserId,
                message);

            return (null, "stripe.account_refused",
                message ?? "Stripe could not open a payout account for you.");
        }

        return (ReadString(payload!, "id"), null, null);
    }

    private async Task<(string? Url, string? Code, string? Message)> MintLinkAsync(
        string account,
        PayoutOnboardingRequest request,
        StripeOptions settings,
        CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("account", account),
            new("refresh_url", request.RefreshUrl),
            new("return_url", request.ReturnUrl),
            new("type", "account_onboarding")
        };

        // No idempotency key: a link is single-use and short-lived, so replaying one would send a
        // member to a page that has already been spent.
        var (payload, ok, transport) = await SendAsync(
            "/v1/account_links", form, idempotencyKey: null, settings, cancellationToken);

        if (transport is not null)
        {
            return (null, transport.Value.Code, transport.Value.Message);
        }

        if (!ok)
        {
            var message = ReadNested(payload!, "error", "message");

            _logger.LogWarning(
                "Stripe refused an onboarding link for account {Account}: {Message}",
                account,
                message);

            return (null, "stripe.link_refused",
                message ?? "Stripe could not open the setup page.");
        }

        return (ReadString(payload!, "url"), null, null);
    }

    private async Task<(string? Payload, bool Ok, (string Code, string Message)? Transport)> SendAsync(
        string path,
        IReadOnlyList<KeyValuePair<string, string>> form,
        string? idempotencyKey,
        StripeOptions settings,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.ApiBase.TrimEnd('/')}{path}")
        {
            Content = new FormUrlEncodedContent(form)
        };

        StripeSettlementProvider.Authorize(message, settings);

        if (idempotencyKey is not null)
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Stripe could not be reached for {Path}.", path);

            return (null, false, ("stripe.unreachable",
                "Stripe could not be reached. Try again in a moment."));
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return (payload, response.IsSuccessStatusCode, null);
        }
    }

    // ── Reading ─────────────────────────────────────────────────────────────────────────────

    private static string? ReadString(string payload, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty(property, out var element)
                ? element.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stripe's own words for a refusal.
    /// <para>
    /// Surfaced rather than replaced, because the useful ones are specific — "Connect is not
    /// enabled on this account", "we do not support GB for Express accounts" — and a generic
    /// sentence would send somebody looking in the wrong place.
    /// </para>
    /// </summary>
    private static string? ReadNested(string payload, string outer, string inner)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty(outer, out var parent)
                && parent.TryGetProperty(inner, out var element)
                    ? element.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
