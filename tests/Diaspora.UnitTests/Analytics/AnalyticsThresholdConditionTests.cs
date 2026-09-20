using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Extensibility.Automation;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// The figure an automation threshold compares against — 20 Sep 2026.
/// <para>
/// <b>"No rows" meant two different things, and this source only knew one of them.</b> The
/// projection writes a rollup row only for a day on which something happened, so an empty window
/// belongs equally to a creator analytics has never recorded and to one whose month was simply
/// quiet. This source answered nothing to both, with a stated reason: answering zero to the first
/// would fire "below 100" for everybody the projection had not reached.
/// </para>
/// <para>
/// The reason was right and the flag was wrong. <c>metricBelow</c> exists to notice a figure
/// falling; the moment it falls to zero the rows stop, so the condition went silent at the exact
/// moment it had something to say, and a rule written for a quiet month could never fire in one.
/// <c>HasHistory</c> separates the two cases, and this module drew that distinction on
/// 13 Sep 2026 for the comparison tiles — see <see cref="AnalyticsComparisonHistoryTests"/>, whose
/// defect was the same conflation one layer up.
/// </para>
/// </summary>
public sealed class AnalyticsThresholdConditionTests
{
    private static readonly Guid Owner = Guid.Parse("9b2f7c41-6d05-4e83-a17c-2e8b4f0d6a39");

    private static readonly DateTimeOffset Now = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Inside the fixed thirty-day window this source reads.</summary>
    private static readonly DateOnly InsideWindow = new(2026, 9, 10);

    /// <summary>Well before it, so the window itself is empty.</summary>
    private static readonly DateOnly LongBefore = new(2026, 5, 1);

    private static readonly string[] BothKeys =
        [AutomationConditionKeys.MetricAbove, AutomationConditionKeys.MetricBelow];

    /// <summary>
    /// <b>The defect.</b> A creator who was recorded in May and had a quiet September is a zero, not
    /// a silence — and a silence is what stopped "tell me when my views drop below 100" from ever
    /// firing for the member who most needed it.
    /// </summary>
    [Fact]
    public async Task A_quiet_window_after_earlier_activity_answers_zero()
    {
        var answers = await ReadAsync(recordedOn: LongBefore);

        answers[AutomationConditionKeys.MetricBelow].Should().Be("0");
        answers[AutomationConditionKeys.MetricAbove].Should().Be("0");
    }

    /// <summary>
    /// <b>And the reason the defect existed, which still holds.</b> A creator the projection has
    /// never reached has no figure at all. Answering zero here would fire every "below" rule on the
    /// platform for everybody analytics had not got to yet.
    /// </summary>
    [Fact]
    public async Task A_creator_the_platform_has_never_recorded_is_unanswered()
    {
        var answers = await ReadAsync(recordedOn: null);

        answers.Should().BeEmpty("no rows anywhere is an absent figure, not a zero");
    }

    /// <summary>The ordinary case: rows in the window, and the headline figure they carry.</summary>
    [Fact]
    public async Task A_window_with_rows_answers_what_they_add_up_to()
    {
        var answers = await ReadAsync(recordedOn: InsideWindow, views: 42);

        answers[AutomationConditionKeys.MetricAbove].Should().Be("42");
        answers[AutomationConditionKeys.MetricBelow].Should().Be("42");
    }

    /// <summary>
    /// Both directions take one measurement, and a caller asking for one is not handed the other.
    /// </summary>
    [Fact]
    public async Task Only_the_keys_that_were_asked_for_are_answered()
    {
        var answers = await ReadAsync(
            recordedOn: InsideWindow, views: 7, keys: [AutomationConditionKeys.MetricBelow]);

        answers.Should().ContainKey(AutomationConditionKeys.MetricBelow);
        answers.Should().NotContainKey(AutomationConditionKeys.MetricAbove);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        DateOnly? recordedOn, long views = 0, string[]? keys = null)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var source = new AnalyticsAutomationConditionSource(
            new AnalyticsReadService(
                Rollups(recordedOn, views), Substitute.For<IAnalyticsEventRepository>()),
            clock);

        return await source.ReadAsync(
            new AutomationConditionQuery(new UserId(Owner), keys ?? BothKeys, null),
            CancellationToken.None);
    }

    /// <summary>
    /// A rollup table holding at most one owner row, and answering "was anything recorded before
    /// this date" truthfully — which is the question the whole distinction turns on.
    /// </summary>
    private static IAnalyticsRollupRepository Rollups(DateOnly? recordedOn, long views)
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();

        IReadOnlyList<OwnerDailyRollup> none = [];

        rollups
            .ListOwnerAsync(
                Owner, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => recordedOn is { } day
                && day >= call.ArgAt<DateOnly>(1)
                && day <= call.ArgAt<DateOnly>(2)
                    ? Rows(day, views)
                    : none);

        rollups
            .AnyOwnerBeforeAsync(Owner, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => recordedOn is { } day && day < call.ArgAt<DateOnly>(1));

        return rollups;
    }

    private static IReadOnlyList<OwnerDailyRollup> Rows(DateOnly day, long views)
    {
        var row = OwnerDailyRollup.Create(OwnerDailyRollupId.New(), day, Owner);

        row.Replace(
            AnalyticsCounts.Zero with { Views = views },
            dailyUniqueViewers: views,
            profileViews: 0,
            computedAt: Now);

        return [row];
    }
}
