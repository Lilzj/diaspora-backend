using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Strategies;

/// <summary>
/// One weighted ranking strategy, configured for one surface, as reference data.
/// <para>
/// <b>A strategy's weight is the single most consequential number in the module and the one nobody
/// can predict, which is exactly why it is a database row.</b> Whether trust should outweigh
/// proximity on the housing surface is not knowable from first principles; it is discoverable from
/// the acceptance rates this module already records. Making it configuration closes that loop —
/// measure, retune, measure again — without a release between each turn.
/// </para>
/// <para>
/// <b>One row per surface per strategy, and that is the design rather than an accident of
/// normalisation.</b> Recency deserves 0.4 on the home feed and 0.05 on guides, where the best
/// answer to a visa question is often five years old. A single weight per strategy would force one
/// of those two surfaces to be wrong.
/// </para>
/// <para>
/// <b>The catalogue refuses to enable a strategy whose implementation is not registered.</b> A
/// mistyped key or a strategy kind with no adapter — the machine-learning kinds, until somebody
/// ships one — fails at startup naming the key, rather than contributing silent zeroes to every
/// score on a surface for a month.
/// </para>
/// </summary>
public sealed class RankingStrategy : AggregateRoot<RankingStrategyId>, IAuditableEntity
{
    private readonly List<ConfigurationParameter> _parameters = [];

    private RankingStrategy(
        RankingStrategyId id,
        string key,
        RankingStrategyKind kind,
        RecommendationSurface surface,
        string description,
        decimal weight,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        Kind = kind;
        Surface = surface;
        Description = description;
        Weight = weight;
        CreatedAt = createdAt;
        IsEnabled = true;
    }

    private RankingStrategy() { }

    /// <summary>The key the registry resolves an implementation by.</summary>
    public string Key { get; private set; } = string.Empty;

    public RankingStrategyKind Kind { get; private set; }

    /// <summary>Which surface this configuration applies to. One row per surface — see the class summary.</summary>
    public RecommendationSurface Surface { get; private set; }

    /// <summary>Null applies to every target type on the surface.</summary>
    public RecommendationTargetType? TargetType { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>0–1. What this strategy's raw score is multiplied by before summing.</summary>
    public decimal Weight { get; private set; }

    /// <summary>Lower runs first. Matters only for the strategies that read what earlier ones wrote.</summary>
    public int Order { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Whether this strategy runs on the cold-start path.
    /// <para>
    /// Most do not, and cannot: a similarity strategy given a profile with no history returns zero
    /// for every candidate, which is arithmetic rather than a ranking. The flag lets a surface
    /// declare which of its strategies still mean something when the platform knows nothing.
    /// </para>
    /// </summary>
    public bool RunsOnColdStart { get; private set; }

    /// <summary>Contributions below this are dropped rather than summed. Keeps explanations honest.</summary>
    public decimal MinimumScore { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<ConfigurationParameter> Parameters => _parameters.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static RankingStrategy Configure(
        RankingStrategyId id,
        string key,
        RankingStrategyKind kind,
        RecommendationSurface surface,
        string description,
        decimal weight,
        DateTimeOffset createdAt,
        RecommendationTargetType? targetType = null,
        int order = 100,
        bool runsOnColdStart = false,
        decimal minimumScore = 0m)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new RankingStrategy(
            id,
            key.Trim().ToLowerInvariant(),
            kind,
            surface,
            description.Trim(),
            Math.Clamp(weight, 0m, 1m),
            createdAt)
        {
            TargetType = targetType,
            Order = order,
            RunsOnColdStart = runsOnColdStart,
            MinimumScore = Math.Clamp(minimumScore, 0m, 1m)
        };
    }

    public void SetWeight(decimal weight)
    {
        Weight = Math.Clamp(weight, 0m, 1m);
        Touch();
    }

    public void SetOrder(int order)
    {
        Order = order;
        Touch();
    }

    public void SetMinimumScore(decimal minimumScore)
    {
        MinimumScore = Math.Clamp(minimumScore, 0m, 1m);
        Touch();
    }

    public void SetRunsOnColdStart(bool runsOnColdStart)
    {
        RunsOnColdStart = runsOnColdStart;
        Touch();
    }

    public void Describe(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        Description = description.Trim();
        Touch();
    }

    public void SetParameter(string key, string value, Guid parameterId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = key.Trim().ToLowerInvariant();
        var existing = _parameters.Find(parameter =>
            string.Equals(parameter.Key, normalized, StringComparison.Ordinal));

        if (existing is not null)
        {
            existing.SetValue(value);
        }
        else
        {
            _parameters.Add(ConfigurationParameter.Create(parameterId, normalized, value));
        }

        Touch();
    }

    public void Enable()
    {
        IsEnabled = true;
        Touch();
    }

    public void Disable()
    {
        IsEnabled = false;
        Touch();
    }

    public string? Parameter(string key) =>
        _parameters
            .Find(parameter => string.Equals(
                parameter.Key, key.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            ?.Value;

    /// <summary>
    /// A parameter parsed as a number, with a fallback.
    /// <para>
    /// Parsed here rather than in each strategy so " 0.5 " and "0.5" cannot mean different things
    /// depending on which strategy read them, and so an unparseable value degrades to the documented
    /// default rather than to zero.
    /// </para>
    /// </summary>
    public decimal ParameterOrDefault(string key, decimal fallback) =>
        decimal.TryParse(
            Parameter(key),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void Touch() => Revision++;
}
