using Diaspora.Modules.Analytics.Domain.Enums;

namespace Diaspora.Modules.Analytics.Domain.Policies;

/// <summary>
/// The one place three content-type vocabularies are reconciled.
/// <para>
/// <b>There are genuinely three, and two of them disagree about the word "listing".</b> B6 publishes
/// <c>Listing</c> meaning a service provider and <c>Marketplace</c> meaning a marketplace item; the
/// Phase 7 client's <c>ContentType</c> calls those <c>Provider</c> and <c>Marketplace</c>; the Phase
/// 15 route segment calls a marketplace item <c>listing</c> and does not route providers through the
/// content space at all. A mapper that translated in place would eventually attribute a provider's
/// analytics to a marketplace seller, and it would look right in every code review.
/// </para>
/// <para>
/// Every translation is total and every parse is defensive: an unrecognised string becomes
/// <see cref="AnalyticsContentType.Unknown"/> rather than throwing, because a module that adds a
/// content type must not be able to break ingestion for the ones that already work.
/// </para>
/// </summary>
public static class AnalyticsContentTypes
{
    /// <summary>
    /// Reads B6's published <c>ContentType</c> string.
    /// <para>
    /// <c>Listing</c> is B6's name for a service provider. <c>Comment</c>, <c>Poll</c>,
    /// <c>Member</c> and <c>Other</c> are content types analytics does not attribute performance to
    /// and become <see cref="AnalyticsContentType.Unknown"/> — a comment's engagement belongs to
    /// the comment's author, and this module has no contract that serves it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>Case-insensitive, and that is not defensive coding.</b> B6 emits its enum names
    /// <em>camelCased</em> through <c>CommunityWireNames.ToWire</c> — the wire value is
    /// <c>listing</c>, not <c>Listing</c> — while the same words arrive PascalCased from the client's
    /// own vocabulary. A case-sensitive match against either spelling silently produces
    /// <see cref="AnalyticsContentType.Unknown"/> for every engagement on the platform, and an
    /// unknown content type is a row that still saves, still counts, and never appears on anybody's
    /// dashboard. A test asserts B6's exact wire spellings.
    /// </remarks>
    public static AnalyticsContentType FromCommunity(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "post" => AnalyticsContentType.Post,
            "guide" => AnalyticsContentType.Guide,
            "listing" => AnalyticsContentType.Provider,
            "job" => AnalyticsContentType.Job,
            "housing" => AnalyticsContentType.Housing,
            "marketplace" => AnalyticsContentType.Marketplace,
            "event" => AnalyticsContentType.Event,
            "organization" => AnalyticsContentType.Organization,
            _ => AnalyticsContentType.Unknown
        };

    /// <summary>
    /// Reads the Phase 15 route segment, case-insensitively.
    /// <para>
    /// <c>listing</c> here is a <em>marketplace</em> listing, and <c>update</c> is a community post.
    /// Both differ from B6's reading of the same words, which is the whole reason this file exists.
    /// </para>
    /// </summary>
    public static AnalyticsContentType FromRouteSegment(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "guide" => AnalyticsContentType.Guide,
            "update" or "post" => AnalyticsContentType.Post,
            "event" => AnalyticsContentType.Event,
            "job" => AnalyticsContentType.Job,
            "housing" => AnalyticsContentType.Housing,
            "listing" or "marketplace" => AnalyticsContentType.Marketplace,
            "provider" => AnalyticsContentType.Provider,
            "organization" => AnalyticsContentType.Organization,
            _ => AnalyticsContentType.Unknown
        };

    /// <summary>
    /// The Phase 7 client's <c>ContentType</c> string, which is what
    /// <c>CreatorContentPerformance.contentType</c> carries.
    /// </summary>
    public static string ToClientContentType(AnalyticsContentType value) => value switch
    {
        AnalyticsContentType.Post => "Update",
        AnalyticsContentType.Guide => "Guide",
        AnalyticsContentType.Provider => "Provider",
        AnalyticsContentType.Job => "Job",
        AnalyticsContentType.Housing => "Housing",
        AnalyticsContentType.Marketplace => "Marketplace",
        AnalyticsContentType.Event => "Event",
        AnalyticsContentType.Organization => "Organization",
        _ => "Unknown"
    };

    /// <summary>The Phase 15 subject type a content row reports under.</summary>
    public static string ToSubjectType(AnalyticsContentType value) => value switch
    {
        AnalyticsContentType.Post => "update",
        AnalyticsContentType.Guide => "guide",
        AnalyticsContentType.Provider => "provider",
        AnalyticsContentType.Job => "job",
        AnalyticsContentType.Housing => "housing",
        AnalyticsContentType.Marketplace => "listing",
        AnalyticsContentType.Event => "event",
        AnalyticsContentType.Organization => "organization",
        _ => "unknown"
    };

    /// <summary>
    /// The in-app route a client can open a content row at, where the platform has one.
    /// <para>
    /// Remediation Wave G: these are the *client's* routes, not this module's API prefixes. The
    /// difference is not cosmetic — <c>/guides/{id}</c> and <c>/jobs/{id}</c> are real API paths and
    /// are not screens, so an analytics row linking to either opened nothing.
    /// </para>
    /// </summary>
    public static string? ToClientRoute(AnalyticsContentType value, Guid contentId) => value switch
    {
        AnalyticsContentType.Guide => $"/guide/{contentId}",
        AnalyticsContentType.Post => $"/update/{contentId}",
        AnalyticsContentType.Event => $"/community-life/event/{contentId}",
        AnalyticsContentType.Job => $"/job/{contentId}",
        AnalyticsContentType.Housing => $"/housing/{contentId}",
        AnalyticsContentType.Marketplace => $"/marketplace/{contentId}",
        AnalyticsContentType.Provider => $"/provider/{contentId}",
        AnalyticsContentType.Organization => $"/community-life/org/{contentId}",
        _ => null
    };
}
