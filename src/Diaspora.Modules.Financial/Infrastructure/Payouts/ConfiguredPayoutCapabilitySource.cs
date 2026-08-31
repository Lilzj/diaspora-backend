using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Payouts;

/// <summary>
/// The capability matrix as this deployment configured it.
/// <para>
/// <b>A malformed row is dropped and logged, never thrown.</b> The two alternatives are both worse:
/// failing startup takes an entire API down because somebody typed a country code with three
/// letters, and binding it anyway puts a route in the matrix that matches nothing and looks like a
/// provider outage. Dropping fails closed — the route refuses with a reason — and the log line names
/// the row so it is fixable without a debugger.
/// </para>
/// <para>
/// Reads through <see cref="IOptionsMonitor{TOptions}"/> so a matrix change reaches the router
/// without a restart, matching how the SMS routing table behaves.
/// </para>
/// </summary>
public sealed class ConfiguredPayoutCapabilitySource : IPayoutCapabilitySource
{
    private readonly IOptionsMonitor<PayoutRoutingOptions> _options;
    private readonly ILogger<ConfiguredPayoutCapabilitySource> _logger;

    public ConfiguredPayoutCapabilitySource(
        IOptionsMonitor<PayoutRoutingOptions> options,
        ILogger<ConfiguredPayoutCapabilitySource> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
    }

    public string Name => "configuration";

    public IReadOnlyList<PayoutCapability> GetCapabilities()
    {
        var configured = _options.CurrentValue.Capabilities;

        if (configured is null || configured.Count == 0)
        {
            return [];
        }

        var capabilities = new List<PayoutCapability>(configured.Count);

        for (var index = 0; index < configured.Count; index++)
        {
            if (Convert(configured[index], index) is { } capability)
            {
                capabilities.Add(capability);
            }
        }

        return capabilities;
    }

    private PayoutCapability? Convert(PayoutCapabilityOptions row, int index)
    {
        if (row is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(row.Provider))
        {
            Drop(index, "the provider is empty");
            return null;
        }

        var country = NormalizeCountry(row.Country);

        if (country is null)
        {
            Drop(index, $"'{row.Country}' is not an ISO 3166-1 alpha-2 country code");
            return null;
        }

        var currency = Domain.ValueObjects.Money.NormalizeCurrency(row.Currency);

        if (currency is null)
        {
            Drop(index, $"'{row.Currency}' is not an ISO 4217 currency code");
            return null;
        }

        if (row.MinimumMinorUnits is < 0 || row.MaximumMinorUnits is < 0)
        {
            Drop(index, "a payout limit cannot be negative");
            return null;
        }

        if (row.MinimumMinorUnits is { } minimum
            && row.MaximumMinorUnits is { } maximum
            && minimum > maximum)
        {
            Drop(index, $"the minimum ({minimum}) is above the maximum ({maximum})");
            return null;
        }

        return new PayoutCapability
        {
            Provider = row.Provider.Trim(),
            Country = country,
            Currency = currency,
            Method = row.Method,
            Support = row.Support,
            Enabled = row.Enabled,
            Priority = row.Priority,
            MinimumMinorUnits = row.MinimumMinorUnits,
            MaximumMinorUnits = row.MaximumMinorUnits,
            RequiresVerifiedPayee = row.RequiresVerifiedPayee,
            RequiresProviderOnboarding = row.RequiresProviderOnboarding,
            Note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim()
        };
    }

    private void Drop(int index, string reason) =>
        _logger.LogError(
            "Payout capability row {Index} was dropped because {Reason}. No payout will route "
            + "through it until the configuration is corrected.",
            index,
            reason);

    private static string? NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        var trimmed = country.Trim().ToUpperInvariant();

        return trimmed.Length == 2 && trimmed.All(char.IsAsciiLetterUpper) ? trimmed : null;
    }
}
