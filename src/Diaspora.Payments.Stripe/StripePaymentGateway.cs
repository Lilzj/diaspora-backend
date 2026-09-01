using System.Globalization;
using System.Net;
using System.Text.Json;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Takes a payment with Stripe, behind the Financial Platform's <see cref="IPaymentGateway"/> seam.
/// <para>
/// <b>This is the charge half, and until it existed the deployment had no way to take money at
/// all.</b> The payout adapters that already shipped implement <c>ISettlementProvider</c> — money
/// leaving. <c>GetFinancialCapabilitiesQueryHandler</c> builds <c>gateways</c> and
/// <c>supportedMethods</c> from <c>IPaymentGatewayRegistry</c>, which only ever held
/// <c>SimulatedPaymentGateway</c>, and that one is excluded outside Development and Testing. So a
/// production deployment answered <c>gateways: []</c> truthfully, and every deposit screen in the
/// client read that as "this platform cannot take payments".
/// </para>
/// <para>
/// <b>Payment Intents, and the member's instrument never touches the platform.</b> The intent is
/// created here with <c>capture_method=manual</c> and handed back as a
/// <see cref="GatewayOutcome.RequiresAction"/> carrying the client secret; the app confirms it with
/// Stripe's own SDK, so no card number, token or instrument reaches this process. That is the shape
/// <see cref="PaymentGatewayRequest.ClientContext"/> was designed for, and it is why authorising
/// does not by itself mean money moved.
/// </para>
/// <para>
/// <b>Every operation carries its own idempotency key, and they must never be the same one.</b>
/// Stripe replays the first response it saw for a key. Authorising and capturing the same intent
/// under one key would make the capture return the <em>authorisation's</em> body, which reads as a
/// success and captures nothing. The key is therefore the payment plus the operation.
/// </para>
/// <para>
/// <b>An unknown answer is retryable here, unlike a payout.</b> A transfer that may have been
/// created cannot be repeated safely, which is why <c>StripeSettlementProvider</c> has
/// <c>Unresolved</c>. A charge repeated under the same idempotency key replays instead of charging
/// twice, so a timeout, a 5xx, a 429 or a 409 is <see cref="GatewayOutcome.Retryable"/>. A 4xx that
/// is not one of those is Stripe declining, and no money moved.
/// </para>
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    public const string HttpClientName = "payments.stripe.charges";

    private static readonly HashSet<PaymentMethodKind> Methods = [PaymentMethodKind.Card];

    /// <summary>Empty: the account's own country and agreement decide, not this adapter.</summary>
    private static readonly HashSet<string> AnyCurrency = new(StringComparer.Ordinal);

    private readonly IOptionsMonitor<StripeOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(
        IOptionsMonitor<StripeOptions> options,
        HttpClient http,
        ILogger<StripePaymentGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public IReadOnlySet<PaymentMethodKind> SupportedMethods => Methods;

    public IReadOnlySet<string> SupportedCurrencies => AnyCurrency;

    /// <summary>
    /// True. A card intent can be authorised and captured separately, which is what lets the
    /// platform hold an amount while an order or a booking is still cancellable.
    /// </summary>
    public bool SupportsSeparateAuthorization => true;

    public async Task<PaymentGatewayResult> AuthorizeAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = Settings();

        if (!settings.IsCheckoutConfigured)
        {
            // A key without return URLs can still pay people out; it cannot take a payment, because
            // Stripe requires both URLs to open a Checkout. Saying so names the missing setting
            // instead of failing further in with a 400 nobody can place.
            return PaymentGatewayResult.Unavailable(
                $"'{StripeOptions.SectionName}:CheckoutSuccessUrl' and ':CheckoutCancelUrl' are not "
                + "configured, so no hosted Checkout can be opened.");
        }

        var reference = request.PaymentIntentId.ToString("N");

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),

            // Manual, so completing Checkout holds rather than takes. The platform's payment path
            // decides when to capture, and an adapter that captured here would silently remove that
            // choice from every caller.
            new("payment_intent_data[capture_method]", "manual"),

            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][unit_amount]",
                request.Amount.MinorUnits.ToString(CultureInfo.InvariantCulture)),

            // Stripe takes lower-case ISO 4217; Money stores it upper-case, and sending it that way
            // is rejected as an unknown currency rather than normalised.
            new("line_items[0][price_data][currency]", request.Amount.Currency.ToLowerInvariant()),
            new("line_items[0][price_data][product_data][name]", Truncate(request.Description, 250)),

            new("success_url", settings.CheckoutSuccessUrl),
            new("cancel_url", settings.CheckoutCancelUrl),

            // The correlation that makes this work. A Checkout session has no Stripe PaymentIntent
            // id until the member pays, so its callback cannot be matched on one. It carries this
            // instead, and ProcessPaymentWebhookCommandHandler falls back to finding an intent by
            // the platform identifier when the external reference does not resolve.
            new("client_reference_id", reference),

            new("metadata[diaspora_payment_intent]", reference),
            new("payment_intent_data[metadata][diaspora_payment_intent]", reference),
        };

        if (request.PayerUserId is { } payer)
        {
            form.Add(new KeyValuePair<string, string>(
                "metadata[diaspora_payer]", payer.ToString("N")));
        }

        var attempt = await SendAsync(
            $"{settings.ApiBase.TrimEnd('/')}/v1/checkout/sessions",
            form,
            IdempotencyKey(request, "authorize"),
            settings,
            request.PaymentIntentId,
            "authorize",
            cancellationToken).ConfigureAwait(false);

        if (attempt.Failure is not null)
        {
            return attempt.Failure;
        }

        var sessionId = ReadString(attempt.Payload!, "id");
        var url = ReadString(attempt.Payload!, "url");

        if (sessionId is null || string.IsNullOrWhiteSpace(url))
        {
            // Without the URL there is nowhere to send the member, and pretending otherwise would
            // leave them on a spinner for ever.
            return PaymentGatewayResult.Unavailable(
                $"Stripe returned a Checkout session with no URL for {request.PaymentIntentId:N}.");
        }

        // The session id is what the platform stores; every later operation resolves it to the
        // PaymentIntent Stripe created underneath. See ResolveIntentAsync.
        return PaymentGatewayResult.Challenge(url, sessionId);
    }

    public async Task<PaymentGatewayResult> CaptureAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = Settings();
        var stored = RequireExternalReference(request, out var missing);

        if (missing is not null)
        {
            return missing;
        }

        var (intentId, unresolved) = await ResolveIntentAsync(
            stored!, settings, request.PaymentIntentId, cancellationToken).ConfigureAwait(false);

        if (unresolved is not null)
        {
            return unresolved;
        }

        // A partial capture is legitimate — an order that shipped short, a booking that shortened.
        // Stripe defaults to the full authorised amount when this is absent, so it is always sent
        // rather than inferred.
        var form = new List<KeyValuePair<string, string>>
        {
            new("amount_to_capture", request.Amount.MinorUnits.ToString(CultureInfo.InvariantCulture)),
        };

        var attempt = await SendAsync(
            $"{settings.ApiBase.TrimEnd('/')}/v1/payment_intents/{Uri.EscapeDataString(intentId!)}/capture",
            form,
            IdempotencyKey(request, "capture"),
            settings,
            request.PaymentIntentId,
            "capture",
            cancellationToken).ConfigureAwait(false);

        return attempt.Failure ?? Interpret(attempt.Payload!, request.PaymentIntentId);
    }

    public async Task<PaymentGatewayResult> CancelAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = Settings();
        var stored = RequireExternalReference(request, out var missing);

        if (missing is not null)
        {
            return missing;
        }

        var (intentId, unresolved) = await ResolveIntentAsync(
            stored!, settings, request.PaymentIntentId, cancellationToken).ConfigureAwait(false);

        if (unresolved is not null)
        {
            return unresolved;
        }

        var attempt = await SendAsync(
            $"{settings.ApiBase.TrimEnd('/')}/v1/payment_intents/{Uri.EscapeDataString(intentId!)}/cancel",
            [],
            IdempotencyKey(request, "cancel"),
            settings,
            request.PaymentIntentId,
            "cancel",
            cancellationToken).ConfigureAwait(false);

        return attempt.Failure ?? Interpret(attempt.Payload!, request.PaymentIntentId);
    }

    public async Task<PaymentGatewayResult> RefundAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = Settings();
        var stored = RequireExternalReference(request, out var missing);

        if (missing is not null)
        {
            return missing;
        }

        var (intentId, unresolved) = await ResolveIntentAsync(
            stored!, settings, request.PaymentIntentId, cancellationToken).ConfigureAwait(false);

        if (unresolved is not null)
        {
            return unresolved;
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("payment_intent", intentId!),
            new("amount", request.Amount.MinorUnits.ToString(CultureInfo.InvariantCulture)),
            new("metadata[diaspora_payment_intent]", request.PaymentIntentId.ToString("N")),
        };

        var attempt = await SendAsync(
            $"{settings.ApiBase.TrimEnd('/')}/v1/refunds",
            form,
            IdempotencyKey(request, "refund"),
            settings,
            request.PaymentIntentId,
            "refund",
            cancellationToken).ConfigureAwait(false);

        if (attempt.Failure is not null)
        {
            return attempt.Failure;
        }

        // A refund has its own status vocabulary — succeeded, pending, failed, canceled — and
        // reading it with the intent's would call a pending refund an unrecognised state.
        var status = ReadString(attempt.Payload!, "status");
        var refundId = ReadString(attempt.Payload!, "id");

        if (refundId is null)
        {
            // Same reasoning as an intent with no id: a refund the platform cannot name is one it
            // can never reconcile against Stripe's own record of it.
            return PaymentGatewayResult.Unavailable(
                $"Stripe returned a refund with no id for {request.PaymentIntentId:N}.");
        }

        return status switch
        {
            "failed" or "canceled" => PaymentGatewayResult.Declined(
                $"stripe.refund_{status}",
                ReadFailureReason(attempt.Payload!) ?? "Stripe did not complete the refund."),

            // `pending` included: the refund is accepted and on its way, and calling that a failure
            // would have the platform tell a member their refund was refused while it was in flight.
            _ => PaymentGatewayResult.Success(refundId, status),
        };
    }

    // ── Interpretation ───────────────────────────────────────────────────────

    /// <summary>
    /// Turns a Payment Intent body into the platform's vocabulary.
    /// <para>
    /// <b><c>requires_payment_method</c> is not a decline.</b> It is the ordinary state of an intent
    /// the member has not paid yet, and reading it as a failure would fail every first attempt. It
    /// comes back as a challenge carrying the client secret, which is what the app needs to finish.
    /// </para>
    /// </summary>
    internal static PaymentGatewayResult Interpret(string payload, Guid paymentIntentId)
    {
        var status = ReadString(payload, "status");
        var intentId = ReadString(payload, "id");

        if (intentId is null)
        {
            // Every Stripe object carries an id. A body without one is not a shape this adapter
            // knows how to act on, and returning success would record a payment the platform could
            // never capture, cancel or refund because it could not name it.
            return PaymentGatewayResult.Unavailable(
                $"Stripe returned a body with no intent id for {paymentIntentId:N}.");
        }

        switch (status)
        {
            case "succeeded":
            case "requires_capture":
            case "canceled":
                return PaymentGatewayResult.Success(intentId, status);

            case "processing":
                // Stripe has it and has not finished. The webhook or a later read settles it; what
                // matters here is that it is neither a decline nor money the platform may spend.
                return PaymentGatewayResult.Success(intentId, status);

            case "requires_payment_method":
            case "requires_confirmation":
            case "requires_action":
                var secret = ReadString(payload, "client_secret");

                if (string.IsNullOrWhiteSpace(secret))
                {
                    // Without the secret the app cannot continue, and pretending otherwise would
                    // leave a member on a spinner for ever.
                    return PaymentGatewayResult.Unavailable(
                        "Stripe returned an intent with no client secret.");
                }

                return PaymentGatewayResult.Challenge(secret, intentId);

            default:
                return PaymentGatewayResult.Unavailable(
                    $"Stripe returned an unrecognised intent status '{status}' for {paymentIntentId:N}.");
        }
    }

    // ── Transport ────────────────────────────────────────────────────────────

    private readonly record struct Attempt(string? Payload, PaymentGatewayResult? Failure);

    private async Task<Attempt> SendAsync(
        string url,
        IReadOnlyCollection<KeyValuePair<string, string>> form,
        string idempotencyKey,
        StripeOptions settings,
        Guid paymentIntentId,
        string operation,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };

        StripeSettlementProvider.Authorize(request, settings);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Stripe could not be reached to {Operation} payment {PaymentIntentId}. The same "
                + "idempotency key makes a retry safe.",
                operation,
                paymentIntentId);

            return new Attempt(null, PaymentGatewayResult.Unavailable("Stripe could not be reached."));
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new Attempt(payload, null);
            }

            var detail = ReadError(payload);

            if (IsRetryable(response.StatusCode))
            {
                _logger.LogWarning(
                    "Stripe answered {Status} to {Operation} for payment {PaymentIntentId}: {Detail}. "
                    + "Retrying under the same idempotency key replays rather than charges twice.",
                    (int)response.StatusCode,
                    operation,
                    paymentIntentId,
                    detail);

                return new Attempt(null, new PaymentGatewayResult
                {
                    Outcome = GatewayOutcome.Retryable,
                    FailureCode = $"stripe.http_{(int)response.StatusCode}",
                    FailureMessage = detail,
                });
            }

            _logger.LogWarning(
                "Stripe refused to {Operation} payment {PaymentIntentId} with {Status}: {Detail}",
                operation,
                paymentIntentId,
                (int)response.StatusCode,
                detail);

            return new Attempt(null, PaymentGatewayResult.Declined(
                ReadDeclineCode(payload) ?? $"stripe.http_{(int)response.StatusCode}", detail));
        }
    }

    private StripeOptions Settings()
    {
        var settings = _options.CurrentValue;

        if (!settings.IsConfigured)
        {
            // Unreachable through the registration, which does not register an unconfigured adapter.
            // Kept because "not registered" and "registered without a key" fail very differently and
            // only one of them is obvious from a log line.
            throw new InvalidOperationException(
                $"'{StripeOptions.SectionName}:SecretKey' is not configured.");
        }

        return settings;
    }

    /// <summary>
    /// The intent Stripe issued. Everything after authorisation addresses it by that id, which the
    /// platform carries on <see cref="PaymentGatewayRequest.ExternalReference"/>.
    /// </summary>
    /// <summary>
    /// Turns whatever the platform stored into the Stripe PaymentIntent id.
    /// <para>
    /// <b>Authorising stores a Checkout <c>cs_...</c>, and no Stripe PaymentIntent exists at that
    /// moment</b> - it is created when the member pays. So capture, cancel and refund each begin by
    /// reading the session for the <c>payment_intent</c> it produced. A reference that is already a
    /// <c>pi_...</c> passes straight through, which keeps this correct for an intent created any
    /// other way.
    /// </para>
    /// </summary>
    private async Task<(string? IntentId, PaymentGatewayResult? Failure)> ResolveIntentAsync(
        string reference,
        StripeOptions settings,
        Guid paymentIntentId,
        CancellationToken cancellationToken)
    {
        if (!reference.StartsWith("cs_", StringComparison.Ordinal))
        {
            return (reference, null);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{settings.ApiBase.TrimEnd('/')}/v1/checkout/sessions/{Uri.EscapeDataString(reference)}");

        StripeSettlementProvider.Authorize(request, settings);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Stripe could not be reached to resolve Checkout session {Session} for payment "
                + "{PaymentIntentId}.",
                reference,
                paymentIntentId);

            return (null, PaymentGatewayResult.Unavailable("Stripe could not be reached."));
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return (null, IsRetryable(response.StatusCode)
                    ? new PaymentGatewayResult
                    {
                        Outcome = GatewayOutcome.Retryable,
                        FailureCode = $"stripe.http_{(int)response.StatusCode}",
                        FailureMessage = ReadError(payload),
                    }
                    : PaymentGatewayResult.Declined(
                        $"stripe.http_{(int)response.StatusCode}", ReadError(payload)));
            }

            var intentId = ReadString(payload, "payment_intent");

            if (string.IsNullOrWhiteSpace(intentId))
            {
                // The member has not completed Checkout, so there is nothing to capture, cancel or
                // refund. A refusal with a reason, not an error.
                return (null, PaymentGatewayResult.Declined(
                    "stripe.checkout_not_completed",
                    "The member has not completed the Stripe Checkout for that payment."));
            }

            return (intentId, null);
        }
    }

    private static string? RequireExternalReference(
        PaymentGatewayRequest request, out PaymentGatewayResult? failure)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            failure = PaymentGatewayResult.Declined(
                "stripe.no_intent_reference",
                "No Stripe payment intent is recorded for that payment.");

            return null;
        }

        failure = null;
        return request.ExternalReference.Trim();
    }

    /// <summary>
    /// Namespaced by operation, because Stripe replays the first response it saw for a key and a
    /// capture that replayed its authorisation would report success while capturing nothing.
    /// </summary>
    internal static string IdempotencyKey(PaymentGatewayRequest request, string operation) =>
        request.IdempotencyKey is { Length: > 0 } supplied
            ? $"diaspora-payment-{operation}-{supplied}"
            : $"diaspora-payment-{operation}-{request.PaymentIntentId:N}";

    /// <summary>Stripe's own instrument tokens. Anything else is context this adapter did not issue.</summary>
    private static bool IsPaymentMethodToken(string? context) =>
        context is { Length: > 3 }
        && context.Trim().StartsWith("pm_", StringComparison.Ordinal);

    /// <summary>
    /// Statuses after which the charge may or may not exist, and a retry under the same key is the
    /// safe move. 409 is included because Stripe returns it when an idempotency key is reused while
    /// the first request is still in flight.
    /// </summary>
    private static bool IsRetryable(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.Conflict
            or HttpStatusCode.TooManyRequests
        || (int)status >= 500;

    private static string Truncate(string value, int limit) =>
        string.IsNullOrEmpty(value) || value.Length <= limit ? value : value[..limit];

    // ── Reading Stripe's bodies ──────────────────────────────────────────────

    internal static string? ReadString(string payload, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind is JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The issuer's reason in preference to Stripe's own, because <c>insufficient_funds</c> tells a
    /// member what to do and <c>card_declined</c> does not.
    /// </summary>
    private static string? ReadDeclineCode(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return null;
            }

            foreach (var property in (string[])["decline_code", "code"])
            {
                if (error.TryGetProperty(property, out var value)
                    && value.ValueKind is JsonValueKind.String
                    && value.GetString() is { Length: > 0 } code)
                {
                    return $"stripe.{code}";
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadFailureReason(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty("failure_reason", out var reason)
                && reason.ValueKind is JsonValueKind.String
                    ? reason.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && message.ValueKind is JsonValueKind.String
                && message.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // Falls through to the sentence below. An unparseable body is still a refusal; it just
            // cannot explain itself, and inventing a reason would be worse than saying so.
        }

        return "Stripe refused the request and did not explain why.";
    }
}
