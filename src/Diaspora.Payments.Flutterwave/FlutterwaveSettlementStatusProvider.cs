using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Flutterwave;

/// <summary>
/// Asks Flutterwave what became of a transfer the platform is unsure about.
/// <para>
/// <b>Flutterwave can be asked by the platform's own reference, which is why this exists and
/// PayPal's equivalent does not.</b> PayPal identifies a payout by the batch id it returns, so a
/// submission whose response was lost has nothing to look up; Flutterwave's transfers list accepts
/// the reference the submission set, which survives any lost response.
/// </para>
/// <para>
/// <b>An empty list after the grace window is a refusal; anything unreadable is not.</b> Flutterwave
/// positively holding no transfer for a reference means nothing left the platform and the hold may
/// go back. A 401, an unparseable body or an unreachable host must stay unresolved — reading a bad
/// key as "no transfer exists" would release every held payout on the deployment.
/// </para>
/// </summary>
public sealed class FlutterwaveSettlementStatusProvider : ISettlementStatusProvider
{
    public const string HttpClientName = "payments.flutterwave.transfers";

    private readonly IOptionsMonitor<FlutterwaveOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<FlutterwaveSettlementStatusProvider> _logger;

    public FlutterwaveSettlementStatusProvider(
        IOptionsMonitor<FlutterwaveOptions> options,
        HttpClient http,
        ILogger<FlutterwaveSettlementStatusProvider> logger)
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
                "flutterwave.no_reference", "Nothing to look the transfer up by.");
        }

        var settings = _options.CurrentValue;

        // The same namespaced reference the submission sent, not the bare withdrawal id.
        var lookup = $"diaspora-payout-{reference.Trim()}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{settings.ApiBase.TrimEnd('/')}/v3/transfers?reference={Uri.EscapeDataString(lookup)}");

        FlutterwaveSettlementProvider.Authorize(request, settings);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception, "Flutterwave could not be asked about settlement {Reference}.", reference);

            return SettlementOutcome.Unresolved(
                "flutterwave.status_unreachable", "Flutterwave could not be reached.");
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Flutterwave answered {Status} when asked about settlement {Reference}.",
                    (int)response.StatusCode,
                    reference);

                return SettlementOutcome.Unresolved(
                    $"flutterwave.status_http_{(int)response.StatusCode}",
                    "Flutterwave would not say what became of it.");
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
                "flutterwave.status_unreadable", "Flutterwave's answer could not be parsed.");
        }

        var envelope = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : null;

        if (!string.Equals(envelope, "success", StringComparison.OrdinalIgnoreCase))
        {
            return SettlementOutcome.Unresolved(
                "flutterwave.status_refused", FlutterwaveSettlementProvider.ReadMessage(payload));
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return SettlementOutcome.Unresolved(
                "flutterwave.status_unreadable", "Flutterwave's answer had no transfer list.");
        }

        foreach (var transfer in data.EnumerateArray())
        {
            var id = transfer.TryGetProperty("id", out var idElement)
                ? idElement.ValueKind == JsonValueKind.Number
                    ? idElement.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : idElement.GetString()
                : null;

            var transferStatus = transfer.TryGetProperty("status", out var element)
                ? element.GetString()?.ToUpperInvariant()
                : null;

            return transferStatus switch
            {
                "FAILED" => SettlementOutcome.Refused(
                    "flutterwave.failed", "Flutterwave reports the transfer failed."),

                "SUCCESSFUL" or "NEW" or "PENDING" => SettlementOutcome.Accepted(id),

                _ => SettlementOutcome.Unresolved(
                    "flutterwave.unrecognised_status", $"Transfer status '{transferStatus}'.")
            };
        }

        // Empty, after the grace window: Flutterwave holds no transfer for this withdrawal, so
        // nothing left and the hold may go back.
        return SettlementOutcome.Refused(
            "flutterwave.transfer_absent", $"Flutterwave holds no transfer for {reference}.");
    }
}
