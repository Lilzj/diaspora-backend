using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Recommendations.Observability;

/// <summary>
/// The module's activity source and instruments.
/// <para>
/// <b>Created here and declared in the observability contributor, always together.</b> Declaring a
/// source without creating one subscribes the tracer to silence, which is the B2 mistake every
/// module since has been careful not to repeat.
/// </para>
/// <para>
/// <b>What is counted is what somebody asks when the feed looks wrong.</b> "Are pages being
/// generated" (<see cref="SetsGenerated"/>), "is anybody acting on them"
/// (<see cref="FeedbackRecorded"/> tagged by kind), "are we ranking nothing"
/// (<see cref="EmptyGenerations"/>), and three counters that catch configuration mistakes nothing
/// else would surface: a policy excluding everything, a strategy with no implementation, and a
/// candidate pool that came back empty.
/// </para>
/// </summary>
public static class RecommendationTelemetry
{
    public const string SourceName = "Diaspora.Recommendations";

    public static readonly ActivitySource Activity = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> SetsGenerated =
        Meter.CreateCounter<long>("diaspora.recommendations.sets.generated");

    public static readonly Counter<long> SetsReused =
        Meter.CreateCounter<long>("diaspora.recommendations.sets.reused");

    public static readonly Counter<long> ItemsGenerated =
        Meter.CreateCounter<long>("diaspora.recommendations.items.generated");

    public static readonly Counter<long> ColdStartGenerations =
        Meter.CreateCounter<long>("diaspora.recommendations.coldstart.generations");

    /// <summary>
    /// A generation that produced nothing.
    /// <para>
    /// Expected in a country the platform has just launched in, and a defect anywhere else. A rise
    /// on an established surface is the first sign of a policy misconfiguration.
    /// </para>
    /// </summary>
    public static readonly Counter<long> EmptyGenerations =
        Meter.CreateCounter<long>("diaspora.recommendations.generations.empty");

    /// <summary>Candidates removed, tagged by the policy that removed them.</summary>
    public static readonly Counter<long> PolicyExclusions =
        Meter.CreateCounter<long>("diaspora.recommendations.policy.exclusions");

    /// <summary>A configured strategy nothing implements. Always a mistake.</summary>
    public static readonly Counter<long> UnimplementedStrategies =
        Meter.CreateCounter<long>("diaspora.recommendations.strategies.unimplemented");

    /// <summary>A candidate pool that came back with nothing to rank.</summary>
    public static readonly Counter<long> EmptyCandidatePools =
        Meter.CreateCounter<long>("diaspora.recommendations.candidates.empty");

    public static readonly Counter<long> SignalsRecorded =
        Meter.CreateCounter<long>("diaspora.recommendations.signals.recorded");

    /// <summary>Signals a privacy preference dropped. A rise means members are opting out.</summary>
    public static readonly Counter<long> SignalsDropped =
        Meter.CreateCounter<long>("diaspora.recommendations.signals.dropped");

    public static readonly Counter<long> ProfilesBuilt =
        Meter.CreateCounter<long>("diaspora.recommendations.profiles.built");

    public static readonly Counter<long> ProfilesRebuilt =
        Meter.CreateCounter<long>("diaspora.recommendations.profiles.rebuilt");

    public static readonly Counter<long> CandidatesProjected =
        Meter.CreateCounter<long>("diaspora.recommendations.candidates.projected");

    public static readonly Counter<long> CandidatesWithdrawn =
        Meter.CreateCounter<long>("diaspora.recommendations.candidates.withdrawn");

    /// <summary>
    /// Paid placements applied to a candidate, tagged by target type.
    /// <para>
    /// Counted separately from projections precisely so an operator can answer "how much of this
    /// page is paid for" without inspecting rows. A promotion counter that climbed while
    /// <c>candidates.projected</c> stood still would be the first sign that placements are landing
    /// on subjects nothing is publishing.
    /// </para>
    /// </summary>
    public static readonly Counter<long> CandidatesPromoted =
        Meter.CreateCounter<long>("diaspora.recommendations.candidates.promoted");

    public static readonly Counter<long> PromotionsEnded =
        Meter.CreateCounter<long>("diaspora.recommendations.candidates.promotion_ended");

    /// <summary>Feedback, tagged by kind. The numerator of every rate the module reports.</summary>
    public static readonly Counter<long> FeedbackRecorded =
        Meter.CreateCounter<long>("diaspora.recommendations.feedback.recorded");

    public static readonly Histogram<double> GenerationDuration =
        Meter.CreateHistogram<double>("diaspora.recommendations.generation.duration", "ms");

    public static readonly Histogram<double> ProfileBuildDuration =
        Meter.CreateHistogram<double>("diaspora.recommendations.profile.build.duration", "ms");

    /// <summary>How many candidates a ranking pass scored. The performance budget's input.</summary>
    public static readonly Histogram<long> CandidatePoolSize =
        Meter.CreateHistogram<long>("diaspora.recommendations.candidates.pool_size");
}
