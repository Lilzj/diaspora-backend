using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Flutterwave;

/// <summary>
/// Sends a Flutterwave transfer to a bank account or a mobile-money handle.
/// <para>
/// <b>The destination token carries two values, because Flutterwave needs two.</b> A transfer names
/// a bank and an account within it, and <c>PayoutDestination.ProviderToken</c> is one string — so
/// the deployment stores them as <c>bankCode:accountNumber</c> and this adapter splits them. A
/// token in any other shape is refused rather than guessed at: sending an account number with no
/// bank produces a transfer to whatever Flutterwave decides that means.
/// </para>
/// <para>
/// <b>The reference is the withdrawal's own identifier, and it is what makes a repeat safe.</b>
/// Flutterwave rejects a transfer whose reference it has already seen, and — unlike PayPal — its
/// transfers API can be queried by that same reference. That is why this provider has a status
/// source and PayPal does not: an unknown outcome here can be resolved without an operator.
/// </para>
/// <para>
/// <b>The payload shape here is written from Flutterwave's v3 transfers API and has not been
/// executed against a live account.</b> Until somebody runs the live sandbox test with real
/// credentials, every capability row for this provider should stay <c>Unknown</c> so nothing routes
/// to it. The unit tests pin the request this adapter builds, so a correction is a visible diff.
/// </para>
/// </summary>
public sealed class FlutterwaveSettlementProvider : ISettlementProvider
{
    public const string HttpClientName = "payments.flutterwave";

    private readonly IOptionsMonitor<FlutterwaveOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<FlutterwaveSettlementProvider> _logger;

    public FlutterwaveSettlementProvider(
        IOptionsMonitor<FlutterwaveOptions> options,
        HttpClient http,
        ILogger<FlutterwaveSettlementProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<SettlementOutcome> SubmitAsync(
        SettlementInstruction instruction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        var settings = _options.CurrentValue;

        if (!IsOurs(instruction.DestinationProvider, settings.Provider))
        {
            return SettlementOutcome.Refused(
                "flutterwave.destination_not_ours",
                "The payout destination was issued by another provider.");
        }

        var (bank, account) = SplitDestination(instruction.DestinationReference);

        if (bank is null || account is null)
        {
            return SettlementOutcome.Refused(
                "flutterwave.destination_malformed",
                "The destination must be recorded as 'bankCode:accountNumber'.");
        }

        var body = BuildTransfer(instruction, bank, account, settings.DebitCurrency);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.ApiBase.TrimEnd('/')}/v3/transfers")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        Authorize(request, settings);

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
                "Flutterwave could not be reached for settlement {Reference}. Whether the transfer "
                + "was created is unknown.",
                instruction.Reference);

            return SettlementOutcome.Unresolved(
                "flutterwave.unreachable", "Flutterwave could not be reached; the transfer may exist.");
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (IsUnknownOutcome(response.StatusCode))
            {
                _logger.LogWarning(
                    "Flutterwave answered {Status} for settlement {Reference}. The outcome is "
                    + "unknown and the hold stays.",
                    (int)response.StatusCode,
                    instruction.Reference);

                return SettlementOutcome.Unresolved(
                    $"flutterwave.http_{(int)response.StatusCode}", ReadMessage(payload));
            }

            return Read(payload, response.IsSuccessStatusCode, (int)response.StatusCode);
        }
    }

    /// <summary>
    /// Turns one transfer response into an outcome.
    /// <para>
    /// <b>Flutterwave answers 200 with <c>status: "error"</c>, so the HTTP code alone decides
    /// nothing.</b> An adapter that trusted <c>IsSuccessStatusCode</c> would report a declined
    /// transfer as paid and mark the withdrawal complete against money that never left.
    /// </para>
    /// <para>
    /// A transfer Flutterwave has accepted is <c>NEW</c> or <c>PENDING</c> rather than
    /// <c>SUCCESSFUL</c> — it settles asynchronously. Both are "accepted": the instruction is with
    /// the provider and the money is committed. <c>FAILED</c> is a refusal.
    /// </para>
    /// </summary>
    internal static SettlementOutcome Read(string payload, bool httpSucceeded, int statusCode)
    {
        JsonElement root;

        try
        {
            // The document owns a pooled buffer and must be returned; the clone is what
            // outlives it. Parsing and dereferencing in one expression leaked the buffer
            // on every webhook (CA2026, BARCH-003).
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // A body that cannot be read says nothing about whether a transfer was created.
            return SettlementOutcome.Unresolved(
                "flutterwave.unreadable", "Flutterwave's answer could not be parsed.");
        }

        var envelope = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : null;

        var message = ReadMessage(payload);

        if (!httpSucceeded || !string.Equals(envelope, "success", StringComparison.OrdinalIgnoreCase))
        {
            return SettlementOutcome.Refused($"flutterwave.http_{statusCode}", message);
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return SettlementOutcome.Unresolved(
                "flutterwave.no_data", "Flutterwave reported success with no transfer.");
        }

        var id = data.TryGetProperty("id", out var idElement)
            ? idElement.ValueKind == JsonValueKind.Number
                ? idElement.GetInt64().ToString(CultureInfo.InvariantCulture)
                : idElement.GetString()
            : null;

        var transferStatus = data.TryGetProperty("status", out var transferStatusElement)
            ? transferStatusElement.GetString()
            : null;

        return transferStatus?.ToUpperInvariant() switch
        {
            "FAILED" => SettlementOutcome.Refused("flutterwave.failed", message),
            "SUCCESSFUL" or "NEW" or "PENDING" => SettlementOutcome.Accepted(id),

            // An unrecognised state is not read as either. Flutterwave adding a value must not make
            // this adapter start releasing holds or completing payouts on it.
            _ => SettlementOutcome.Unresolved(
                "flutterwave.unrecognised_status", $"Transfer status '{transferStatus}'.")
        };
    }

    internal static string BuildTransfer(
        SettlementInstruction instruction, string bank, string account, string debitCurrency)
    {
        // Flutterwave takes a decimal amount, not minor units. The exponent comes off the amount so
        // a zero-decimal currency is not sent with a spurious ".00".
        var amount = instruction.Amount.Amount.ToString(
            $"F{instruction.Amount.Exponent}", CultureInfo.InvariantCulture);

        var document = new Dictionary<string, object?>
        {
            ["account_bank"] = bank,
            ["account_number"] = account,
            ["amount"] = amount,
            ["currency"] = instruction.Amount.Currency,
            ["narration"] = "Diaspora payout",

            // Ours, and the handle the status provider looks the transfer up by.
            ["reference"] = TransferReference(instruction)
        };

        if (!string.IsNullOrWhiteSpace(instruction.DestinationLabel))
        {
            document["beneficiary_name"] = instruction.DestinationLabel;
        }

        if (!string.IsNullOrWhiteSpace(debitCurrency))
        {
            document["debit_currency"] = debitCurrency;
        }

        return JsonSerializer.Serialize(document);
    }

    /// <summary>
    /// The reference a repeat must reuse, and the one a status lookup uses. Namespaced because
    /// Flutterwave scopes it to the account.
    /// </summary>
    internal static string TransferReference(SettlementInstruction instruction) =>
        $"diaspora-payout-{instruction.Reference ?? instruction.CounterpartyId.ToString("N")}";

    internal static void Authorize(HttpRequestMessage request, FlutterwaveOptions settings) =>
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.SecretKey);

    /// <summary>
    /// Splits <c>bankCode:accountNumber</c>. Both null when the token is not in that shape — see the
    /// type summary for why this refuses rather than improvises.
    /// </summary>
    internal static (string? Bank, string? Account) SplitDestination(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, null);
        }

        var parts = token.Split(':', 2, StringSplitOptions.TrimEntries);

        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? (parts[0], parts[1])
            : (null, null);
    }

    private static bool IsOurs(string? issuer, string provider) =>
        string.IsNullOrWhiteSpace(issuer)
        || string.Equals(issuer.Trim(), provider, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnknownOutcome(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || (int)status >= 500;

    internal static string ReadMessage(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString() ?? "unspecified"
                : "unspecified";
        }
        catch (JsonException)
        {
            return "unparseable body";
        }
    }
}
