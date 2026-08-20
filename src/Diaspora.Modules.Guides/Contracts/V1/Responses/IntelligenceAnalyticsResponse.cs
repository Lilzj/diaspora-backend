namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// How much one country's official information is actually being used.
/// <para>
/// <b>Totals, never people.</b> Every number here is a count of events. There is no field for who,
/// no session, no cohort — the brief asks whether advisories are being seen, and that question is
/// answerable from totals. This surface is gated on an analytics permission and would still be safe
/// if it were not, because there is nothing here to leak.
/// </para>
/// <para>
/// <b>Approximate by contract.</b> The counters are written by a sink that swallows its own failures
/// rather than break a read, so a number here can be slightly low. An editorial team reading "did
/// anybody see the cyclone advisory" is served far better by an approximate number than by a page
/// that failed to load.
/// </para>
/// </summary>
public sealed record IntelligenceAnalyticsResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required long NoticeViews { get; init; }

    /// <summary>Readers who followed the official source or a related link.</summary>
    public required long ClickThroughs { get; init; }

    /// <summary>Readers who reached the end. The signal the summary was not enough.</summary>
    public required long ReadCompletions { get; init; }

    /// <summary>Dashboard opens. The country-interest signal.</summary>
    public required long DashboardViews { get; init; }

    public required long EmbassyViews { get; init; }

    /// <summary>Emergency information opens. A rising count is a country in trouble.</summary>
    public required long EmergencyViews { get; init; }

    public required long VisaViews { get; init; }

    public required long HolidayViews { get; init; }

    /// <summary>Readers who crossed from a notice into a guide.</summary>
    public required long GuideCrossNavigations { get; init; }

    public required long Searches { get; init; }

    /// <summary>Searches that returned nothing. The commissioning signal.</summary>
    public required long UnansweredSearches { get; init; }

    /// <summary>Opens that arrived from something B14 recommended.</summary>
    public required long RecommendationsAccepted { get; init; }

    /// <summary>Opens that arrived from a notification B13 sent. The delivery correlation.</summary>
    public required long NotificationsFollowed { get; init; }

    /// <summary>
    /// Click-through rate over notice views, as a percentage, or null when nothing was viewed.
    /// <para>
    /// Computed here rather than left to a client, because two clients computing it would eventually
    /// disagree about the denominator — and a rate whose denominator nobody agrees on is a number an
    /// editorial team will make a decision on and be wrong.
    /// </para>
    /// </summary>
    public double? ClickThroughRate { get; init; }

    /// <inheritdoc cref="ClickThroughRate" />
    public double? ReadCompletionRate { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    /// <summary>The most-read notices, with their view counts.</summary>
    public IReadOnlyList<OfficialNoticeCardResponse> MostViewed { get; init; } = [];
}
