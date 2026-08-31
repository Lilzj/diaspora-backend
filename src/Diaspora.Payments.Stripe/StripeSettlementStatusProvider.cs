using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Asks Stripe what became of a transfer the platform is unsure about.
/// <para>
/// <b>This is how an unknown outcome stops being unknown.</b> A withdrawal left in
/// <c>Processing</c> is money held against a payout that may or may not have happened, and holding
/// it is only the safe half of the answer — somebody still has to find out. Stripe can be asked,
/// so it is.
/// </para>
/// <para>
/// <b>It looks the transfer up by <c>transfer_group</c>, which the submission already set to the
/// withdrawal's identifier.</b> That is the one handle the platform is guaranteed to still have: a
/// submission that timed out never returned a transfer id, so a lookup keyed on one could not find
/// exactly the transfers this exists to find.
/// </para>
/// <para>
/// <b>"No transfer found" is reported as a refusal, and that is the one judgement call here.</b>
/// Stripe answering with an empty list after the grace window has passed is a positive statement
/// that it holds no such transfer — the request never landed — so the hold may be released. The
/// window matters: asked too early, an empty list means "not yet", and acting on it would fail a
/// payout that was about to succeed. <c>WithdrawalResolutionGraceMinutes</c> is what keeps the two
/// apart, and it is why this returns <c>Unresolved</c> for anything it cannot read cleanly.
/// </para>
/// </summary>
public sealed class StripeSettlementStatusProvider : ISettlementStatusProvider
{
    public const string HttpClientName = "payments.stripe.transfers";

    private readonly IOptionsMonitor<StripeOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<StripeSettlementStatusProvider> _logger;

    public StripeSettlementStatusProvider(
        IOptionsMonitor<StripeOptions> options,
        HttpClient http,
        ILogger<StripeSettlementStatusProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<SettlementOutcome> GetOutcomeAsync(
        string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return SettlementOutcome.Unresolved(
                "stripe.no_reference", "Nothing to look the transfer up by.");
        }

        var settings = _options.CurrentValue;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{settings.ApiBase.TrimEnd('/')}/v1/transfers"
            + $"?transfer_group={Uri.EscapeDataString(reference.Trim())}&limit=1");

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
                exception, "Stripe could not be asked about settlement {Reference}.", reference);

            return SettlementOutcome.Unresolved(
                "stripe.status_unreachable", "Stripe could not be reached.");
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Including a 401. A wrong key must not read as "no transfer exists", which would
                // release every held payout on the deployment.
                _logger.LogWarning(
                    "Stripe answered {Status} when asked about settlement {Reference}.",
                    (int)response.StatusCode,
                    reference);

                return SettlementOutcome.Unresolved(
                    $"stripe.status_http_{(int)response.StatusCode}",
                    "Stripe would not say what became of it.");
            }

            return Read(payload, reference);
        }
    }

    internal static SettlementOutcome Read(string payload, string reference)
    {
        JsonElement root;

        try
        {
            root = JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return SettlementOutcome.Unresolved(
                "stripe.status_unreadable", "Stripe's answer could not be parsed.");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return SettlementOutcome.Unresolved(
                "stripe.status_unreadable", "Stripe's answer had no transfer list.");
        }

        foreach (var transfer in data.EnumerateArray())
        {
            var id = transfer.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;

            // A transfer exists for this withdrawal. Whether Stripe has since reversed it is a
            // separate fact the platform records as a reversal, not as a failure to pay.
            return SettlementOutcome.Accepted(id);
        }

        // Empty, after the grace window: Stripe holds no transfer for this withdrawal, so nothing
        // left the platform and the hold may go back. See the type summary for why the window is
        // what makes this safe to say.
        return SettlementOutcome.Refused(
            "stripe.transfer_absent", $"Stripe holds no transfer for {reference}.");
    }
}
