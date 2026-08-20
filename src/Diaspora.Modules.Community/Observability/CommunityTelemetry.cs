using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Community.Observability;

/// <summary>
/// The module's live instruments.
/// <para>
/// Declared alongside the contributor that subscribes to them, and both are required —
/// declaring a source name with nothing behind it subscribes the tracer to silence, which is the
/// mistake the module extension guide names.
/// </para>
/// <para>
/// The instruments answer the questions this module will actually be asked in production, and
/// they are not the obvious ones. "How many posts" is a vanity metric. <see cref="FeedLatency"/>
/// tagged by feed key tells you <em>which</em> feed is slow, which is the only version of that
/// question anybody can act on. <see cref="AutoHides"/> should be a small number and a spike in
/// it is either an abuse wave or a brigading campaign — two opposite responses, and an
/// undifferentiated moderation counter tells you neither. And
/// <see cref="UnreviewedContent"/> is the honest measure of a deployment with no review provider
/// registered: it should be zero in production and will not be until somebody registers one.
/// </para>
/// </summary>
public static class CommunityTelemetry
{
    public const string SourceName = "Diaspora.Community";

    public const string Version = "1.0.0";

    public static readonly ActivitySource Activity = new(SourceName, Version);

    public static readonly Meter Meter = new(SourceName, Version);

    /// <summary>Posts published, tagged by category and urgency.</summary>
    public static readonly Counter<long> PostsPublished =
        Meter.CreateCounter<long>(
            "diaspora.community.posts_published",
            unit: "{post}",
            description: "Posts that became visible.");

    /// <summary>Comments added, tagged by content type. Shows which verticals adopted B6.</summary>
    public static readonly Counter<long> CommentsAdded =
        Meter.CreateCounter<long>(
            "diaspora.community.comments_added",
            unit: "{comment}",
            description: "Comments added, tagged by the content type commented on.");

    /// <summary>Reactions, tagged by key and sentiment.</summary>
    public static readonly Counter<long> ReactionsRecorded =
        Meter.CreateCounter<long>(
            "diaspora.community.reactions_recorded",
            unit: "{reaction}",
            description: "Reactions added or withdrawn, tagged by key.");

    /// <summary>Feed pages served, tagged by feed key and ranking.</summary>
    public static readonly Counter<long> FeedsServed =
        Meter.CreateCounter<long>(
            "diaspora.community.feeds_served",
            unit: "{page}",
            description: "Feed pages served, tagged by feed and ranking.");

    /// <summary>
    /// Feed latency, tagged by feed key.
    /// <para>
    /// Tagged rather than aggregate, because "the feed is slow" is unactionable and "the nearby
    /// feed is slow" points straight at the bounding-box prefilter.
    /// </para>
    /// </summary>
    public static readonly Histogram<double> FeedLatency =
        Meter.CreateHistogram<double>(
            "diaspora.community.feed_latency",
            unit: "ms",
            description: "Time to compose one feed page.");

    /// <summary>Reports filed, tagged by reason.</summary>
    public static readonly Counter<long> ReportsFiled =
        Meter.CreateCounter<long>(
            "diaspora.community.reports_filed",
            unit: "{report}",
            description: "Reports filed, tagged by reason.");

    /// <summary>
    /// Content hidden automatically by the report threshold.
    /// <para>
    /// Should be small. A spike is either a genuine abuse wave or a brigading campaign, and the
    /// two need opposite responses — which is why this is separate from the moderation counter.
    /// </para>
    /// </summary>
    public static readonly Counter<long> AutoHides =
        Meter.CreateCounter<long>(
            "diaspora.community.auto_hides",
            unit: "{content}",
            description: "Content hidden automatically by the report threshold.");

    /// <summary>Moderator decisions, tagged by action and source.</summary>
    public static readonly Counter<long> ModerationDecisions =
        Meter.CreateCounter<long>(
            "diaspora.community.moderation_decisions",
            unit: "{decision}",
            description: "Moderation decisions, tagged by action and source.");

    /// <summary>
    /// Content that no review provider could assess.
    /// <para>
    /// Non-zero means the deployment has no automated review registered. A legitimate
    /// configuration, and exactly the sort of thing that otherwise goes unnoticed until somebody
    /// asks why nothing is ever flagged.
    /// </para>
    /// </summary>
    public static readonly Counter<long> UnreviewedContent =
        Meter.CreateCounter<long>(
            "diaspora.community.unreviewed_content",
            unit: "{content}",
            description: "Content no registered review provider could assess.");

    /// <summary>Posts swept to expired by the sweeper.</summary>
    public static readonly Counter<long> PostsExpired =
        Meter.CreateCounter<long>(
            "diaspora.community.posts_expired",
            unit: "{post}",
            description: "Posts that passed their expiry and left the feeds.");

    /// <summary>Visibility denials, tagged by reason. A rising count is worth investigating.</summary>
    public static readonly Counter<long> VisibilityDenials =
        Meter.CreateCounter<long>(
            "diaspora.community.visibility_denials",
            unit: "{denial}",
            description: "Reads refused by a visibility evaluator, tagged by reason.");
}
