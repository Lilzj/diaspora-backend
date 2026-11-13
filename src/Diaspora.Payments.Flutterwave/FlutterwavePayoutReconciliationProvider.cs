using System.Globalization;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Flutterwave;

/// <summary>
/// Fetches Flutterwave's own record of the transfers it made, so the platform can be checked
/// against it.
/// <para>
/// <b>This is the adapter that makes reconciliation more than a seam.</b> Without one,
/// <c>PayoutReconciliationSweep</c> runs and finds nothing — and the finding it exists for, a
/// transfer Flutterwave made that no withdrawal accounts for, stays invisible.
/// </para>
/// <para>
/// <b>It pages and filters by date here rather than asking Flutterwave to.</b> The transfers list
/// takes a page number; which query parameters narrow it by date was not confirmed against current
/// documentation, and a filter that silently matched nothing would make the sweep report a clean
/// bill of health on an empty result. Paging and filtering on <c>created_at</c> is slower and cannot
/// be quietly wrong. If the date parameters are confirmed later, this becomes a smaller request and
/// the tests below do not change.
/// </para>
/// <para>
/// <b>The page cap is logged when it is hit.</b> A reconciliation pass that silently stopped early
/// would report "nothing unaccounted for" having looked at a third of the window, which is worse
/// than not running at all.
/// </para>
/// </summary>
public sealed class FlutterwavePayoutReconciliationProvider : IPayoutReconciliationProvider
{
    public const string HttpClientName = "payments.flutterwave.reconciliation";

    /// <summary>
    /// How many pages one pass will read. A window of 72 hours on a busy deployment is well inside
    /// this; a deployment that outgrows it sees the warning rather than a quiet half-check.
    /// </summary>
    private const int MaxPages = 50;

    private readonly IOptionsMonitor<FlutterwaveOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<FlutterwavePayoutReconciliationProvider> _logger;

    public FlutterwavePayoutReconciliationProvider(
        IOptionsMonitor<FlutterwaveOptions> options,
        HttpClient http,
        ILogger<FlutterwavePayoutReconciliationProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<IReadOnlyList<PayoutReconciliationRecord>> FetchAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var settings = _options.CurrentValue;
        var records = new List<PayoutReconciliationRecord>();

        for (var page = 1; page <= MaxPages; page++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{settings.ApiBase.TrimEnd('/')}/v3/transfers"
                + $"?page={page.ToString(CultureInfo.InvariantCulture)}");

            FlutterwaveSettlementProvider.Authorize(request, settings);

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Thrown rather than swallowed: the sweep catches per provider and logs, and a
                // partial page set reported as a complete one would be a reconciliation that found
                // nothing because it stopped looking.
                throw new InvalidOperationException(
                    $"Flutterwave answered {(int)response.StatusCode} for transfers page {page}.");
            }

            var (batch, exhausted) = ReadPage(settings.Provider, payload, from, to);

            records.AddRange(batch);

            if (exhausted)
            {
                return records;
            }

            if (page == MaxPages)
            {
                _logger.LogWarning(
                    "Stopped reading Flutterwave transfers at the {Cap}-page cap. The window "
                    + "{From:o}–{To:o} was not fully examined and an unaccounted-for payout in it "
                    + "would not have been found.",
                    MaxPages,
                    from,
                    to);
            }
        }

        return records;
    }

    /// <summary>
    /// Turns one page into records, and says whether reading further is pointless.
    /// <para>
    /// Flutterwave returns transfers newest first, so a page whose every row predates the window
    /// means every later page does too. Returning early on that is the difference between reading
    /// three pages and reading fifty.
    /// </para>
    /// </summary>
    internal static (List<PayoutReconciliationRecord> Records, bool Exhausted) ReadPage(
        string provider, string payload, DateTimeOffset from, DateTimeOffset to)
    {
        var records = new List<PayoutReconciliationRecord>();

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
            return (records, true);
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return (records, true);
        }

        var rows = 0;
        var allBeforeWindow = true;

        foreach (var transfer in data.EnumerateArray())
        {
            rows++;

            var createdAt = ReadDate(transfer, "created_at");

            if (createdAt is null || createdAt > from)
            {
                // Unknown dates count as "not before the window": a row this adapter cannot date
                // must not be the reason it stops reading.
                allBeforeWindow = false;
            }

            if (createdAt is null || createdAt < from || createdAt > to)
            {
                continue;
            }

            if (Map(provider, transfer, createdAt.Value) is { } record)
            {
                records.Add(record);
            }
        }

        // An empty page is the end. A full page of rows older than the window is also the end,
        // because the list is newest first.
        return (records, rows == 0 || allBeforeWindow);
    }

    /// <summary>
    /// One transfer, or null when it is in a state reconciliation has no opinion about.
    /// <para>
    /// <c>NEW</c> and <c>PENDING</c> are left out deliberately: a transfer still moving is not a
    /// discrepancy, and reporting one would have the sweep flag every in-flight payout as a
    /// mismatch every time it ran.
    /// </para>
    /// </summary>
    internal static PayoutReconciliationRecord? Map(
        string provider, JsonElement transfer, DateTimeOffset occurredAt)
    {
        var status = transfer.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()?.ToUpperInvariant()
            : null;

        var normalized = status switch
        {
            "SUCCESSFUL" => "paid",
            "FAILED" => "failed",
            "REVERSED" => "reversed",
            _ => null
        };

        if (normalized is null)
        {
            return null;
        }

        var currency = transfer.TryGetProperty("currency", out var currencyElement)
            ? currencyElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        var id = transfer.TryGetProperty("id", out var idElement)
            ? idElement.ValueKind == JsonValueKind.Number
                ? idElement.GetInt64().ToString(CultureInfo.InvariantCulture)
                : idElement.GetString()
            : null;

        return new PayoutReconciliationRecord
        {
            Provider = provider,
            ProviderReference = id ?? "unknown",

            // Flutterwave echoes back the reference the transfer was created with. Left namespaced:
            // the sweep unwraps it, and stripping it here would hide a reference that was never
            // ours from the one check that would notice.
            Reference = transfer.TryGetProperty("reference", out var reference)
                ? reference.GetString()
                : null,

            // Minor units, from the decimal amount Flutterwave reports. Two decimals is wrong for a
            // zero-decimal currency and this adapter does not know the exponent — the sweep compares
            // against the withdrawal's own net, so a mismatch here surfaces as a finding rather than
            // as a silently accepted figure.
            AmountMinorUnits = ReadMinorUnits(transfer, "amount"),
            Currency = currency.ToUpperInvariant(),
            Status = normalized,
            OccurredAt = occurredAt
        };
    }

    private static long ReadMinorUnits(JsonElement transfer, string property)
    {
        if (!transfer.TryGetProperty(property, out var element))
        {
            return 0;
        }

        var amount = element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(
                element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            _ => 0m
        };

        return decimal.ToInt64(Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static DateTimeOffset? ReadDate(JsonElement transfer, string property) =>
        transfer.TryGetProperty(property, out var element)
        && element.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(
            element.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
