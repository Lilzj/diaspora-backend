using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Infrastructure.Signals;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Recommendations.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// The immutable record everything else is derived from.
/// <para>
/// <b>If these properties are not true, nothing above them is trustworthy.</b> A profile is only as
/// honest as the signals behind it, and a signal that could be edited after the fact would make every
/// personalization decision unreconstructable.
/// </para>
/// </summary>
public class SignalTests
{
    /// <summary>
    /// <b>A signal exposes no setter for anything it says.</b> The one mutable field is the
    /// processed stamp, which is bookkeeping — see <c>UserSignal</c>.
    /// </summary>
    [Fact]
    public void A_signal_has_no_public_setters()
    {
        var settable = typeof(UserSignal)
            .GetProperties()
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => property.Name)
            .ToList();

        settable.Should().BeEmpty(
            "a derived profile is only trustworthy if the record it derives from cannot be edited");
    }

    /// <summary>
    /// <b>Half-life decay rather than a window.</b> A window means a member's interests change
    /// discontinuously on the ninetieth night, and that somebody away for a season comes back to a
    /// platform that has forgotten them entirely.
    /// </summary>
    [Fact]
    public void Effective_weight_halves_over_one_half_life()
    {
        var signal = Record(weight: 1m, occurredAt: RecommendationTestData.Now.AddDays(-30));

        var effective = signal.EffectiveWeight(
            RecommendationTestData.Now, TimeSpan.FromDays(30));

        effective.Should().BeApproximately(0.5m, 0.01m);
    }

    [Fact]
    public void Effective_weight_never_reaches_zero()
    {
        var signal = Record(weight: 1m, occurredAt: RecommendationTestData.Now.AddYears(-3));

        signal.EffectiveWeight(RecommendationTestData.Now, TimeSpan.FromDays(30))
            .Should().BeGreaterThanOrEqualTo(0m);
    }

    /// <summary>Weight is clamped at recording time, whatever a caller claims.</summary>
    [Theory]
    [InlineData(5, 1)]
    [InlineData(-2, 0)]
    public void Weight_is_clamped_to_the_unit_interval(int supplied, int expected)
    {
        Record(weight: supplied).Weight.Should().Be(expected);
    }

    /// <summary>
    /// <b>Marking a signal processed is idempotent.</b> The builder folds it and stamps it; a repeat
    /// pass must not move the stamp, or a retried build would look like a fresh observation.
    /// </summary>
    [Fact]
    public void Marking_processed_twice_keeps_the_first_moment()
    {
        var signal = Record();

        signal.MarkProcessed(RecommendationTestData.Now);
        signal.MarkProcessed(RecommendationTestData.Now.AddHours(1));

        signal.ProcessedAt.Should().Be(RecommendationTestData.Now);
    }

    /// <summary>Retention is stamped at recording time so the sweep has an indexed column to find.</summary>
    [Fact]
    public void A_signal_carries_its_own_expiry()
    {
        var signal = Record(retention: TimeSpan.FromDays(365));

        signal.ExpiresAt.Should().Be(RecommendationTestData.Now.AddDays(365));
    }

    // ── The weight policy ────────────────────────────────────────────────

    /// <summary>
    /// <b>The ladder: attention, then interest, then commitment.</b> A member who applied for one job
    /// and viewed thirty should be recommended things like the one, not like the thirty.
    /// </summary>
    [Fact]
    public void Commitment_outweighs_attention()
    {
        var policy = new DefaultSignalWeightPolicy();

        policy.WeightFor(SignalKind.Transacted)
            .Should().BeGreaterThan(policy.WeightFor(SignalKind.Applied));

        policy.WeightFor(SignalKind.Applied)
            .Should().BeGreaterThan(policy.WeightFor(SignalKind.Saved));

        policy.WeightFor(SignalKind.Saved)
            .Should().BeGreaterThan(policy.WeightFor(SignalKind.Viewed));
    }

    /// <summary>
    /// <b>A client cannot claim commitment.</b> The ceiling is what stops a modified app inflating
    /// its own member's interests by recording signals that say they transacted.
    /// </summary>
    [Fact]
    public void A_client_cannot_claim_more_than_attention()
    {
        var policy = new DefaultSignalWeightPolicy();

        policy.CeilingFor(SignalSource.Client)
            .Should().BeLessThan(policy.WeightFor(SignalKind.Applied));

        policy.CeilingFor(SignalSource.Jobs)
            .Should().BeGreaterThanOrEqualTo(policy.WeightFor(SignalKind.Transacted));
    }

    /// <summary>
    /// <b>Different kinds go stale at different rates, by two orders of magnitude.</b> A country
    /// lasts years; a search term is worthless in a fortnight, because somebody who searched for an
    /// airport transfer has taken the flight.
    /// </summary>
    [Fact]
    public void A_search_term_ages_far_faster_than_a_country()
    {
        var policy = new DefaultSignalWeightPolicy();

        policy.HalfLifeFor(SignalKind.Searched)
            .Should().BeLessThan(policy.HalfLifeFor(SignalKind.Location) / 10);
    }

    /// <summary>Every kind has a weight and a half-life. A missing one would silently become a default.</summary>
    [Theory]
    [MemberData(nameof(AllSignalKinds))]
    public void Every_signal_kind_is_weighted_and_aged(SignalKind kind)
    {
        var policy = new DefaultSignalWeightPolicy();

        policy.WeightFor(kind).Should().BeInRange(0m, 1m);
        policy.HalfLifeFor(kind).Should().BeGreaterThan(TimeSpan.Zero);
    }

    public static TheoryData<SignalKind> AllSignalKinds()
    {
        var data = new TheoryData<SignalKind>();

        foreach (var kind in Enum.GetValues<SignalKind>())
        {
            data.Add(kind);
        }

        return data;
    }

    private static UserSignal Record(
        decimal weight = 0.5m,
        DateTimeOffset? occurredAt = null,
        TimeSpan? retention = null) =>
        UserSignal.Record(
            UserSignalId.New(),
            UserId.New(),
            SignalKind.Viewed,
            SignalSource.Client,
            weight,
            occurredAt ?? RecommendationTestData.Now,
            RecommendationTestData.Now,
            retention ?? TimeSpan.FromDays(365),
            context: SignalContext.Create("MU"));
}
