using System.Globalization;
using System.Text;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;

namespace Diaspora.Modules.Analytics.Application.Exports;

/// <summary>
/// Writes a dataset as one CSV table.
/// <para>
/// <b>One table, three kinds of row.</b> A <c>metric</c> row is a headline figure with its baseline
/// and change; a <c>series</c> row is one point of a chart, dated; an <c>unavailable</c> row names a
/// figure that was asked for and could not be answered, so a spreadsheet reader sees the gap rather
/// than a missing line they might read as zero. Percentages are ratios, exactly as on the wire.
/// </para>
/// <para>
/// <b>Formula-safe.</b> A cell that a spreadsheet would read as a formula is prefixed with an
/// apostrophe. Every label here is the platform's own today, but the writer does not rely on that:
/// an export that executes on open is not an export.
/// </para>
/// </summary>
public static class AnalyticsCsvWriter
{
    private static readonly string[] Header =
    [
        "kind", "key", "label", "date", "value", "format", "baseline_value", "change_ratio",
        "subject_type", "subject_id", "range_from", "range_to", "timezone"
    ];

    public static string Write(AnalyticsDatasetResponse dataset, IReadOnlyCollection<string>? onlyMetrics)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var wanted = onlyMetrics is { Count: > 0 }
            ? new HashSet<string>(onlyMetrics, StringComparer.Ordinal)
            : null;

        bool Include(string key) => wanted is null || wanted.Contains(key);

        var builder = new StringBuilder();
        AppendRow(builder, Header);

        var subject = new[]
        {
            dataset.Subject.Type,
            dataset.Subject.Id ?? string.Empty,
            dataset.Range.From,
            dataset.Range.To,
            dataset.Range.Timezone
        };

        foreach (var metric in dataset.Metrics.Where(metric => Include(metric.Key)))
        {
            AppendRow(builder,
            [
                "metric",
                metric.Key,
                metric.Label,
                string.Empty,
                Number(metric.Value.Value),
                metric.Format,
                Number(metric.Comparison?.BaselineValue),
                Number(metric.Comparison?.ChangePercent),
                .. subject
            ]);
        }

        foreach (var series in dataset.Series.Where(series => Include(series.Key)))
        {
            foreach (var point in series.Points)
            {
                AppendRow(builder,
                [
                    "series",
                    series.Key,
                    series.Label,
                    point.At,
                    Number(point.Value),
                    series.Format,
                    Number(point.BaselineValue),
                    string.Empty,
                    .. subject
                ]);
            }
        }

        foreach (var key in dataset.UnavailableMetrics.Where(Include))
        {
            AppendRow(builder,
            [
                "unavailable", key, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, .. subject
            ]);
        }

        return builder.ToString();
    }

    private static string Number(double? value) =>
        value is { } number ? number.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty;

    private static void AppendRow(StringBuilder builder, string[] cells)
    {
        for (var index = 0; index < cells.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(Escape(cells[index]));
        }

        builder.Append("\r\n");
    }

    internal static string Escape(string? cell)
    {
        var value = cell ?? string.Empty;

        // A leading = + - @ (or a tab or carriage return) makes a spreadsheet evaluate the cell. A
        // negative number is a number, not a formula, and is left alone.
        if (value.Length > 0
            && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
