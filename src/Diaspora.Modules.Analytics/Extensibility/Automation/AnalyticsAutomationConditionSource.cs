using System.Globalization;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Analytics.Extensibility.Automation;

/// <summary>
/// B19's answer to the two automation conditions about a figure — B23.x Wave 12.
///
/// <para><b>Wave 11 declared <c>metricAbove</c> and <c>metricBelow</c> unevaluable</b> with the words
/// "analytics figures are B19's and this module has no read port for them", and recorded that the
/// automation platform "answers <c>countAbove</c> where a publisher computed it and fails the other
/// two closed rather than guessing". This is the read port, on B19's side.</para>
///
/// <para><b>Which figure, given the frontend offers no metric picker.</b> The condition's editor is a
/// plain number and the trigger that offers both is <c>creator.performanceThreshold</c> — "my content
/// passes a number I choose". So the figure is the member's own headline engagement over the last
/// thirty days: <c>ContentViews</c>, which is the metric every creator surface leads with and the one
/// a threshold is meaningful against. Answering a metric the member did not pick would be worse than
/// answering none, so the choice is stated here, stated in the capability matrix, and is the only one
/// this source will make until the client offers a picker to disambiguate it.</para>
///
/// <para><b>Thirty days, fixed.</b> An automation runs unattended and a member never sees the period
/// selector, so a window that varied with anything would make the same rule mean different things on
/// different days. Thirty days is the client's own default period.</para>
///
/// <para><b>A creator the platform has never recorded is unanswered; a quiet month is a zero.</b>
/// Answering zero to the first would make "below 100" fire for everybody the projection has not
/// reached, which is the failure mode a threshold condition can least afford. But answering nothing
/// to the second is the mirror of it, and it is the one this source shipped with: the projection
/// writes a row only for a day on which something happened, so a creator whose views fall away has
/// no rows — and <c>metricBelow</c>, which exists to notice a figure falling, went silent at the
/// exact moment it had something to say.</para>
///
/// <para><c>HasHistory</c> is what separates the two, and this module drew that distinction on
/// 13 Sep 2026 after Aisha's dashboard read "Not enough history yet" on every tile of a quiet day.
/// The same conflation, in a source written afterwards, reached for the same wrong flag. See
/// <c>AnalyticsWindowTotals</c>.</para>
/// </summary>
public sealed class AnalyticsAutomationConditionSource : IAutomationConditionSource
{
    /// <summary>
    /// The metric a threshold condition compares against, and the reason it is fixed.
    /// <para>
    /// Named as a constant so the capability matrix, the tests and this implementation cannot drift,
    /// and so the day the client ships a metric picker there is one place that stops being a default.
    /// </para>
    /// </summary>
    public const string ThresholdMetric = AnalyticsMetricCatalog.ContentViews;

    /// <summary>The window, fixed. See the type summary.</summary>
    private const AnalyticsPeriodPreset ThresholdPeriod = AnalyticsPeriodPreset.Last30Days;

    private readonly AnalyticsReadService _reads;
    private readonly IClock _clock;

    public AnalyticsAutomationConditionSource(AnalyticsReadService reads, IClock clock)
    {
        _reads = reads;
        _clock = clock;
    }

    public IReadOnlyList<string> Answers { get; } =
    [
        AutomationConditionKeys.MetricAbove,
        AutomationConditionKeys.MetricBelow
    ];

    public async Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        AutomationConditionQuery request, CancellationToken cancellationToken = default)
    {
        var answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (request.Member.Value == Guid.Empty)
        {
            return answers;
        }

        var wanted = request.ConditionKeys
            .Where(key => Answers.Contains(key, StringComparer.Ordinal))
            .ToArray();

        if (wanted.Length == 0)
        {
            return answers;
        }

        // UTC, deliberately. An unattended run has no device timezone to read, and a window that
        // shifted with wherever the process happened to be would make the same rule mean two things.
        var resolution = AnalyticsPeriod.Resolve(
            ThresholdPeriod, null, null, TimeZoneInfo.Utc, _clock.UtcNow, maximumCustomDays: 366);

        if (resolution.Period is not { } period)
        {
            return answers;
        }

        var totals = await _reads
            .ReadOwnerAsync(request.Member.Value, period, cancellationToken)
            .ConfigureAwait(false);

        if (totals is { HasData: false, HasHistory: false })
        {
            // Never recorded, so there is no figure to give. A quiet window — no rows here, rows
            // before it — falls through and answers the zero it actually is. See the type summary.
            return answers;
        }

        var value = AnalyticsMetricAssembler
            .ValueOf(ThresholdMetric, totals)
            .ToString(CultureInfo.InvariantCulture);

        // Both keys take the same figure. `metricAbove` and `metricBelow` are one measurement with two
        // comparison directions, and the automation evaluator supplies the direction.
        foreach (var key in wanted)
        {
            answers[key] = value;
        }

        return answers;
    }
}
