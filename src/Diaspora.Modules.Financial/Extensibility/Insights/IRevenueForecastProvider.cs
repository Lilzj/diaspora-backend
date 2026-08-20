using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Insights;

/// <summary>
/// Projects revenue forward.
/// <para>
/// Declared with no implementation. Returns a currency-scoped figure because the platform never
/// totals across currencies — a forecast in "money" would be the first place that rule quietly broke.
/// </para>
/// </summary>
public interface IRevenueForecastProvider
{
    Task<Money?> ForecastAsync(
        string currency,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);
}
