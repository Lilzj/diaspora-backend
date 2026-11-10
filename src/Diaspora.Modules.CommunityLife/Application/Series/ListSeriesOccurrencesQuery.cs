using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// One series' dates, windowed and paged.
/// <para>
/// <b>Expanded on the server, always.</b> A client expanding the rule itself would produce
/// occurrence identifiers that exist on one device, and an RSVP, a reminder or an attendance record
/// could not refer to any of them tomorrow.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListSeriesOccurrencesQuery : IQuery<EventOccurrencePageResponse>
{
    public required Guid SeriesId { get; init; }

    /// <summary>The earliest instant to include. Defaults to now — a calendar looks forward.</summary>
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Include dates already held, and drafts. Honoured only for somebody who may manage the series;
    /// everybody else gets the announced, upcoming ones whatever this says.
    /// </summary>
    public bool IncludePast { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
