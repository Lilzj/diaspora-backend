using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Reads a connected account's standing from Stripe.
/// <para>
/// <b><c>payouts_enabled</c> is the answer, and nothing else is.</b> Stripe exposes several fields
/// that look like they mean "this account is fine" — <c>charges_enabled</c>, <c>details_submitted</c>,
/// an empty <c>currently_due</c> — and none of them is a statement that Stripe will send money.
/// <c>details_submitted</c> especially: it means the onboarding form was completed, which is the
/// beginning of a review rather than the end of one.
/// </para>
/// <para>
/// <b>Where this adapter cannot map a state, it reports the cautious one.</b> Stripe's
/// <c>disabled_reason</c> vocabulary changes as its compliance rules do, and an adapter that
/// recognised a fixed list would silently degrade to "fine" as new values appeared. An unrecognised
/// reason is <see cref="PayoutProviderKycStatus.Restricted"/>, which refuses.
/// </para>
/// <para>
/// <b>The account id is the payout destination's token, not the member's user id.</b> This source
/// is asked about a Diaspora user, and Stripe knows nothing about those — so the lookup needs the
/// connected account reference. Resolving that mapping is the deployment's, and until it supplies
/// one this source reports <c>NotStarted</c>, which refuses any route that requires onboarding.
/// See <see cref="IStripeAccountResolver"/>.
/// </para>
/// </summary>
public sealed class StripeConnectVerificationSource : IPayoutProviderVerificationSource
{
    public const string HttpClientName = "payments.stripe.accounts";

    private readonly IOptionsMonitor<StripeOptions> _options;
    private readonly IStripeAccountResolver _accounts;
    private readonly HttpClient _http;
    private readonly ILogger<StripeConnectVerificationSource> _logger;

    public StripeConnectVerificationSource(
        IOptionsMonitor<StripeOptions> options,
        IStripeAccountResolver accounts,
        HttpClient http,
        ILogger<StripeConnectVerificationSource> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _accounts = accounts;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<PayoutProviderVerification> GetAsync(
        Guid payeeUserId, CancellationToken cancellationToken = default)
    {
        var settings = _options.CurrentValue;
        var provider = settings.Provider;

        var accountId = await _accounts
            .ResolveAsync(payeeUserId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(accountId))
        {
            // Nobody has connected an account for this member. NotStarted rather than an error:
            // it is the true state, and it refuses.
            return PayoutProviderVerification.Unknown(provider);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{settings.ApiBase.TrimEnd('/')}/v1/accounts/{Uri.EscapeDataString(accountId.Trim())}");

        StripeSettlementProvider.Authorize(request, settings);

        using var response = await _http
            .SendAsync(request, cancellationToken).ConfigureAwait(false);

        var payload = await response.Content
            .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Logged without the payload: an error body from an accounts call can carry the
            // member's own details, and this line goes to an aggregator.
            _logger.LogWarning(
                "Stripe answered {Status} for a connected account lookup. Treating the payee as "
                + "not onboarded.",
                (int)response.StatusCode);

            return PayoutProviderVerification.Unknown(provider);
        }

        return Map(provider, accountId, payload);
    }

    internal static PayoutProviderVerification Map(
        string provider, string accountId, string payload)
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
            return PayoutProviderVerification.Unknown(provider);
        }

        var requirements = root.TryGetProperty("requirements", out var requirementsElement)
            ? requirementsElement
            : default;

        var due = ReadStrings(requirements, "currently_due")
            .Concat(ReadStrings(requirements, "past_due"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var disabledReason = requirements.ValueKind == JsonValueKind.Object
            && requirements.TryGetProperty("disabled_reason", out var reason)
                ? reason.GetString()
                : null;

        var payoutsEnabled = root.TryGetProperty("payouts_enabled", out var enabled)
            && enabled.ValueKind == JsonValueKind.True;

        return new PayoutProviderVerification
        {
            Provider = provider,
            Status = Classify(payoutsEnabled, disabledReason, due.Count),
            OutstandingRequirements = due,
            Reference = accountId,
            ExpiresAt = ReadDeadline(requirements)
        };
    }

    /// <summary>
    /// Turns Stripe's several booleans into one answer.
    /// <para>
    /// <c>payouts_enabled</c> wins outright: Stripe saying it will send money is the only statement
    /// worth acting on, and an account can carry outstanding future requirements while still being
    /// payable today.
    /// </para>
    /// </summary>
    private static PayoutProviderKycStatus Classify(
        bool payoutsEnabled, string? disabledReason, int outstanding)
    {
        if (payoutsEnabled)
        {
            return PayoutProviderKycStatus.Verified;
        }

        if (string.IsNullOrWhiteSpace(disabledReason))
        {
            return outstanding > 0
                ? PayoutProviderKycStatus.Pending
                : PayoutProviderKycStatus.NotStarted;
        }

        // `rejected.*` is Stripe's terminal family — fraud, terms of service, other. Everything else
        // that disables an account is treated as recoverable, and anything unrecognised lands here
        // too rather than being read as fine. See the type summary.
        return disabledReason.StartsWith("rejected", StringComparison.OrdinalIgnoreCase)
            ? PayoutProviderKycStatus.Rejected
            : PayoutProviderKycStatus.Restricted;
    }

    private static DateTimeOffset? ReadDeadline(JsonElement requirements)
    {
        if (requirements.ValueKind != JsonValueKind.Object
            || !requirements.TryGetProperty("current_deadline", out var deadline)
            || deadline.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(deadline.GetInt64());
    }

    private static IEnumerable<string> ReadStrings(JsonElement parent, string property)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(property, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.GetString() is { Length: > 0 } value)
            {
                yield return value;
            }
        }
    }
}

/// <summary>
/// Turns a Diaspora member into the Stripe connected account that belongs to them.
/// <para>
/// <b>A seam because the mapping is the deployment's, not this adapter's.</b> Where a connected
/// account id lives differs by how onboarding was built — on a payout destination's provider token,
/// in an operator-managed table, in an identity claim — and an adapter that assumed one would be
/// wrong for the others.
/// </para>
/// <para>
/// The shipped implementation resolves nothing, so every account reads as <c>NotStarted</c> and any
/// route requiring onboarding refuses. That is the honest default: the alternative is an adapter
/// that reports members as verified because it could not find out otherwise.
/// </para>
/// </summary>
public interface IStripeAccountResolver
{
    /// <summary>The connected account id for a member, or null when there is none.</summary>
    Task<string?> ResolveAsync(Guid payeeUserId, CancellationToken cancellationToken = default);
}

/// <summary>Resolves nothing. See <see cref="IStripeAccountResolver"/>.</summary>
public sealed class UnmappedStripeAccountResolver : IStripeAccountResolver
{
    public Task<string?> ResolveAsync(
        Guid payeeUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
