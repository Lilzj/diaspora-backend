namespace Diaspora.Modules.Guides.Extensibility.Analytics;

/// <summary>
/// One thing a reader did with the Knowledge Directory.
/// <para>
/// <b>Flat, primitive and free of any reader identity.</b> This crosses a seam that a deployment
/// may point at a warehouse, a product analytics vendor or a log file, and the shape has to survive
/// all three — so no domain type, no aggregate, no identifier of a person. What is here is the
/// smallest thing that answers the questions the brief lists: which country, which subject, which
/// guide, which phrase, how many results.
/// </para>
/// <para>
/// <b>Signals are recorded whether or not anything is listening.</b> B8.1 ships a sink that folds
/// them into the per-country counters, so the dashboard works with no external system configured.
/// A deployment that adds a second sink gets both.
/// </para>
/// </summary>
public sealed record KnowledgeAnalyticsSignal
{
    /// <summary>One of the constants in <see cref="KnowledgeAnalyticsSignalKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2, when the action named a country.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The navigation row involved, when there was one.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>The guide opened or completed, when there was one.</summary>
    public Guid? GuideId { get; init; }

    /// <summary>The learning path involved, when there was one.</summary>
    public Guid? LearningPathId { get; init; }

    /// <summary>The normalized search phrase, for a search signal.</summary>
    public string? Term { get; init; }

    /// <summary>How many results a search returned. The commissioning signal when it is zero.</summary>
    public int? ResultCount { get; init; }

    /// <summary>The reader's locale, when the client sent one. Never an identity.</summary>
    public string? Locale { get; init; }

    public required DateTimeOffset At { get; init; }
}
