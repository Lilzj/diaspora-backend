using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// One series' dates.
/// <para>
/// <b>What the caller may see is decided here, not by the query string.</b> Somebody who may manage
/// the series sees its drafts and its history; everybody else sees the announced dates that have not
/// finished, whatever <c>includePast</c> says. A read that honoured the parameter for anybody would
/// expose an unannounced calendar to a stranger who guessed a URL.
/// </para>
/// </summary>
public sealed class ListSeriesOccurrencesQueryHandler
    : IQueryHandler<ListSeriesOccurrencesQuery, EventOccurrencePageResponse>
{
    private readonly IEventSeriesRepository _series;
    private readonly ICommunityEventRepository _events;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly EventSeriesReadContext _read;
    private readonly IClock _clock;

    public ListSeriesOccurrencesQueryHandler(
        IEventSeriesRepository series,
        ICommunityEventRepository events,
        CommunityLifeAuthorizationService authorization,
        EventSeriesReadContext read,
        IClock clock)
    {
        _series = series;
        _events = events;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<EventOccurrencePageResponse>> Handle(
        ListSeriesOccurrencesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var series = await _series.GetAsync(
            EventSeriesId.From(query.SeriesId), cancellationToken);

        if (series is null)
        {
            return Result.Failure<EventOccurrencePageResponse>(
                CommunityLifeErrors.EventSeriesNotFound);
        }

        var readable = await _authorization.RequireSeriesReadAsync(series, cancellationToken);

        if (readable.IsFailure)
        {
            return Result.Failure<EventOccurrencePageResponse>(readable.Error);
        }

        var isManager = await _authorization.CanReadSeriesAsOrganizerAsync(
            series, cancellationToken);
        var now = _clock.UtcNow;

        // The window's left edge. A manager may ask for the whole history; everybody else starts at
        // the clock, because a public calendar of a weekly service is a list of what is still to
        // come and not an archive of three years of Sundays.
        var from = isManager && query.IncludePast
            ? query.From
            : query.From is { } requested && requested > now ? requested : now;

        var page = await _events.FindOccurrencesAsync(
            series.Id,
            from,
            query.To,
            includeNonPublic: isManager,
            query.Cursor,
            Math.Clamp(
                query.Limit ?? CommunityLifeLimits.DefaultPageSize,
                1,
                CommunityLifeLimits.MaxPageSize),
            cancellationToken);

        var next = await _events.FindNextOccurrenceAsync(series.Id, now, cancellationToken);

        return Result.Success(new EventOccurrencePageResponse
        {
            SeriesId = series.Id.Value,
            Items = await _read.ToOccurrencesAsync(
                series, page.Items, next?.Id, cancellationToken),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }
}
