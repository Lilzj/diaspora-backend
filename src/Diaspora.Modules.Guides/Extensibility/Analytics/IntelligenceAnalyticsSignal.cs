namespace Diaspora.Modules.Guides.Extensibility.Analytics;

/// <summary>
/// One thing somebody did with a country's official information.
/// <para>
/// <b>Flat, primitive and free of any reader identity</b> — the same discipline
/// <see cref="KnowledgeAnalyticsSignal"/> holds to, and for a sharper reason. This signal stream
/// includes people opening domestic-violence hotlines and mental-health lines. A shape that could
/// carry a member identifier would make a behavioural log of that possible, and the only reliable way
/// to guarantee it is never built is to leave nowhere for it to go.
/// </para>
/// <para>
/// <b>A separate signal from B8.1's rather than a shared one.</b> They answer different questions —
/// "are the guides for this country being read" against "did people see the advisory in time" — and a
/// shared record would carry a guide identifier that is meaningless on nine tenths of these and a
/// notice identifier that is meaningless on nine tenths of those.
/// </para>
/// <para>
/// <see cref="Source"/> is what makes delivery correlation possible without this module knowing what
/// a notification is: the client says the tap came from a notification or a recommendation, and the
/// sink counts it.
/// </para>
/// </summary>
public sealed record IntelligenceAnalyticsSignal
{
    /// <summary>One of the constants in <see cref="IntelligenceAnalyticsSignalKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2, when the action named a country.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The notice involved, when there was one.</summary>
    public Guid? OfficialNoticeId { get; init; }

    /// <summary>The notice category involved, when there was one.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>The mission involved, when there was one.</summary>
    public Guid? EmbassyId { get; init; }

    /// <summary>The visa advisory involved, when there was one.</summary>
    public Guid? VisaAdvisoryId { get; init; }

    /// <summary>The guide a reader crossed into, for the cross-navigation signal.</summary>
    public Guid? GuideId { get; init; }

    /// <summary>The normalized search phrase, for a search signal.</summary>
    public string? Term { get; init; }

    /// <summary>How many results a search returned. The commissioning signal when it is zero.</summary>
    public int? ResultCount { get; init; }

    /// <summary>
    /// Where the reader came from — <c>notification</c>, <c>recommendation</c>, <c>dashboard</c>,
    /// <c>search</c>. Free text from the client; counted, never trusted for anything else.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>The reader's locale, when the client sent one. Never an identity.</summary>
    public string? Locale { get; init; }

    public required DateTimeOffset At { get; init; }
}
