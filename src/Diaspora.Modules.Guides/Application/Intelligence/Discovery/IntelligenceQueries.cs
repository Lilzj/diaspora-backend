using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Intelligence.Discovery;

/// <summary>
/// A country's whole intelligence layer.
/// <para>
/// <b>Anonymous, like every read in this phase.</b> A platform that demanded an account before it
/// would show somebody an emergency number has misunderstood what the number is for.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetCountryIntelligenceQuery : IQuery<CountryIntelligenceResponse>
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The reader's locale. Falls back exact→language→default.</summary>
    public string? Locale { get; init; }

    /// <summary>Whether to record a dashboard-viewed signal. Clients that prefetch send false.</summary>
    public bool RecordView { get; init; } = true;
}

/// <summary>
/// One page of official notices.
/// <para>
/// <b>The listing every reader-facing screen and every editorial queue share.</b> Which of the two
/// you get is decided by the caller's role rather than by the endpoint: an editor sees drafts and
/// submissions, everybody else sees published and superseded, and the repository narrows the status
/// filter rather than trusting the query to have set it.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListOfficialNoticesQuery : IQuery<CursorPagedResult<OfficialNoticeCardResponse>>
{
    /// <summary>Upper-case ISO 3166-1 alpha-2. Null lists across every country.</summary>
    public string? CountryCode { get; init; }

    public string? CategoryKey { get; init; }

    /// <summary>PascalCase severity names. Empty means every severity.</summary>
    public IReadOnlyList<string> Severities { get; init; } = [];

    /// <summary>The lowest severity that counts, for "show me anything serious".</summary>
    public string? MinimumSeverity { get; init; }

    public Guid? AuthorityId { get; init; }

    /// <summary>A BCP-47 tag the notice was officially issued in.</summary>
    public string? Language { get; init; }

    /// <summary>Free text over title and summary. Real relevance is the search endpoint's job.</summary>
    public string? Text { get; init; }

    /// <summary>Only notices from an authority Trust has confirmed.</summary>
    public bool OfficialOnly { get; init; }

    /// <summary>Only notices that are guidance right now. What nearly every reader screen sets.</summary>
    public bool ActiveOnly { get; init; }

    /// <summary>Only notices whose expiry has passed. The editorial cleanup queue.</summary>
    public bool ExpiredOnly { get; init; }

    /// <summary>PascalCase status names. Editors only; ignored for everybody else.</summary>
    public IReadOnlyList<string> Statuses { get; init; } = [];

    /// <summary>Include archived notices. Editors only.</summary>
    public bool IncludeArchived { get; init; }

    /// <summary>Kebab sort key. Unknown falls back to the briefing order.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }

    public string? Locale { get; init; }
}

/// <summary>One official notice in full.</summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetOfficialNoticeQuery : IQuery<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }

    public string? Locale { get; init; }
}

/// <summary>The notice category catalogue, ordered.</summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListNoticeCategoriesQuery : IQuery<IReadOnlyList<NoticeCategoryResponse>>
{
    public string? Locale { get; init; }

    /// <summary>Archived rows. Editors only; silently ignored for everybody else.</summary>
    public bool IncludeArchived { get; init; }
}

/// <summary>
/// One page of the embassy directory.
/// <para>
/// The two country codes are separate parameters and neither defaults to the other: "missions in
/// Mauritius" and "missions serving Mauritians" are different lists with almost no overlap.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListEmbassiesQuery : IQuery<CursorPagedResult<EmbassyResponse>>
{
    /// <summary>Missions physically in this country.</summary>
    public string? LocatedIn { get; init; }

    /// <summary>Missions serving this country's nationals.</summary>
    public string? Serving { get; init; }

    public bool VerifiedOnly { get; init; }

    /// <summary>Free text over the mission's name and city.</summary>
    public string? Text { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }

    public string? Locale { get; init; }
}

/// <summary>One mission in full.</summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetEmbassyQuery : IQuery<EmbassyResponse>
{
    public required Guid EmbassyId { get; init; }

    public string? Locale { get; init; }
}

/// <summary>
/// One country's emergency information.
/// <para>
/// The response is shaped to be cacheable offline by a client, which is the right thing for a client
/// to do with it — see <see cref="EmergencyContactsResponse"/>.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published emergency contacts. The one read on the platform most likely to be opened by " +
    "somebody in trouble on a phone they have not signed in on.")]
public sealed record GetEmergencyContactsQuery : IQuery<EmergencyContactsResponse>
{
    public required string CountryCode { get; init; }

    public string? Locale { get; init; }

    public bool RecordView { get; init; } = true;
}

/// <summary>
/// One page of visa advisories for a destination.
/// <para>
/// <see cref="AppliesTo"/> matches advisories naming that nationality <em>and</em> those naming
/// nobody, because both apply — a repository that matched only the exact code would tell most members
/// that no rules exist for them.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListVisaAdvisoriesQuery : IQuery<CursorPagedResult<VisaAdvisoryResponse>>
{
    /// <summary>The destination. Required — an advisory is always about one.</summary>
    public required string CountryCode { get; init; }

    /// <summary>A nationality. Null asks for all of them regardless.</summary>
    public string? AppliesTo { get; init; }

    /// <summary>Only advisories inside their effective window.</summary>
    public bool ActiveOnly { get; init; } = true;

    public string? Cursor { get; init; }

    public int? Limit { get; init; }

    public string? Locale { get; init; }
}

/// <summary>One visa advisory in full.</summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetVisaAdvisoryQuery : IQuery<VisaAdvisoryResponse>
{
    public required Guid VisaAdvisoryId { get; init; }

    public string? Locale { get; init; }
}

/// <summary>
/// One country's public holiday calendar.
/// <para>
/// A year or a range, never both. The year is what a calendar screen asks for; the range is what a
/// dashboard's horizon asks for, and expressing one as the other would make the second awkward.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListPublicHolidaysQuery : IQuery<IReadOnlyList<PublicHolidayResponse>>
{
    public required string CountryCode { get; init; }

    /// <summary>A calendar year. Ignored when a range is given.</summary>
    public int? Year { get; init; }

    /// <summary>The start of a range. Needs <see cref="Until"/>.</summary>
    public DateTimeOffset? From { get; init; }

    /// <inheritdoc cref="From" />
    public DateTimeOffset? Until { get; init; }

    public string? Locale { get; init; }
}

/// <summary>
/// Official notice search, through whichever engine this deployment registered.
/// <para>
/// <b>An unavailable engine is reported as unavailable, never as an empty answer.</b> "No results" on
/// an official notice search reads as "the government has said nothing about this", and telling
/// somebody that when the truth is that an index is down is the worst failure mode this module has.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record SearchOfficialNoticesQuery : IQuery<OfficialNoticeSearchResponse>
{
    public string? Text { get; init; }

    public string? CountryCode { get; init; }

    public IReadOnlyList<string> CategoryKeys { get; init; } = [];

    public Guid? AuthorityId { get; init; }

    /// <summary>PascalCase severity names.</summary>
    public IReadOnlyList<string> Severities { get; init; } = [];

    public string? Language { get; init; }

    public bool OfficialOnly { get; init; }

    public bool ActiveOnly { get; init; }

    public bool ExpiredOnly { get; init; }

    public DateTimeOffset? PublishedFrom { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }

    public string? Locale { get; init; }
}

/// <summary>The bodies that issue for one country.</summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListIssuingAuthoritiesQuery : IQuery<IReadOnlyList<IssuingAuthorityResponse>>
{
    public required string CountryCode { get; init; }

    /// <summary>PascalCase authority type. Null lists every kind.</summary>
    public string? AuthorityType { get; init; }

    public bool VerifiedOnly { get; init; }
}

/// <summary>One issuing authority.</summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetIssuingAuthorityQuery : IQuery<IssuingAuthorityResponse>
{
    public required Guid IssuingAuthorityId { get; init; }
}

/// <summary>What this deployment's Country Intelligence Platform can actually do.</summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetIntelligenceCapabilitiesQuery : IQuery<IntelligenceCapabilitiesResponse>
{
    public string? Locale { get; init; }
}

/// <summary>
/// How much one country's official information is being used.
/// <para>
/// Gated at <see cref="IntelligencePermissions.ReadAnalytics"/> — and it would be safe without the
/// gate, because there is nothing here to leak. Every number is a count of events and there is no
/// field for who.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ReadAnalytics)]
public sealed record GetIntelligenceAnalyticsQuery : IQuery<IntelligenceAnalyticsResponse>
{
    public required string CountryCode { get; init; }
}
