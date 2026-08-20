using Diaspora.Modules.CommunityLife.Application.Series;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// The recurring-event surface.
/// <para>
/// <b>Under <c>/events/series</c> and <c>/events/occurrences</c> rather than at a prefix of their
/// own</b>, because a series is a generator of events and an occurrence <em>is</em> an event: an
/// occurrence identifier is an event identifier, and a client holding one can call
/// <c>/events/{id}</c>, RSVP against it and read its attendees with no further ceremony. Two
/// parallel prefixes would have implied two parallel resources.
/// </para>
/// <para>
/// <b>The literal segments sit safely beside <c>/events/{idOrSlug}</c>.</b> ASP.NET Core's route
/// table prefers a literal over a parameter, which is the same reason <c>/events/mine</c> has worked
/// since B12 shipped.
/// </para>
/// </summary>
internal static class EventSeriesEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapReads(group);
        MapWrites(group);
        MapOccurrences(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group.MapGet("/events/series", async (
                [AsParameters] EventSeriesSearchParameters parameters,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(parameters.ToQuery(), cancellationToken)))
            .AllowAnonymous()
            .WithName("SearchEventSeries")
            .WithSummary("Repeating events, filtered and paged.");

        group.MapGet("/events/series/{idOrSlug}", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        Guid.TryParse(idOrSlug, out var id)
                            ? new GetEventSeriesQuery { SeriesId = id }
                            : new GetEventSeriesQuery { Slug = idOrSlug },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetEventSeries")
            .WithSummary("One repeating event, by identifier or by link handle.");

        group.MapGet("/events/series/{seriesId:guid}/occurrences", async (
                Guid seriesId,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] bool? includePast,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListSeriesOccurrencesQuery
                        {
                            SeriesId = seriesId,
                            From = from,
                            To = to,
                            IncludePast = includePast ?? false,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListSeriesOccurrences")
            .WithSummary("One repeating event's dates, expanded by the server.");
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        group.MapPost("/events/series", async (
                CreateEventSeriesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateEventSeriesCommand { Request = request }, cancellationToken)))
            .WithName("CreateEventSeries")
            .WithSummary("Sets up a repeating event and writes its first dates.");

        group.MapPut("/events/series/{seriesId:guid}", async (
                Guid seriesId,
                UpdateEventSeriesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateEventSeriesCommand { SeriesId = seriesId, Request = request },
                        cancellationToken)))
            .WithName("UpdateEventSeries")
            .WithSummary("Edits a repeating event at one of three scopes.");

        group.MapPost("/events/series/{seriesId:guid}/publish", async (
                Guid seriesId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishEventSeriesCommand { SeriesId = seriesId }, cancellationToken)))
            .WithName("PublishEventSeries")
            .WithSummary("Announces a repeating event and the dates already written for it.");

        // DELETE, and it cancels rather than deletes. The frontend's own contract calls it a
        // cancellation, and it is the right verb for the resource: a pattern people are coming to
        // must not vanish, because the people coming to it have to be told.
        group.MapDelete("/events/series/{seriesId:guid}", async (
                Guid seriesId,
                [FromQuery] string? reason,
                [FromQuery] bool? pauseOnly,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelEventSeriesCommand
                        {
                            SeriesId = seriesId,
                            Request = new CancelEventSeriesRequest
                            {
                                Reason = reason,
                                PauseOnly = pauseOnly ?? false
                            }
                        },
                        cancellationToken)))
            .WithName("CancelEventSeries")
            .WithSummary("Calls a repeating event off, or pauses it.");
    }

    private static void MapOccurrences(RouteGroupBuilder group)
    {
        group.MapGet("/events/occurrences/{occurrenceId:guid}", async (
                Guid occurrenceId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOccurrenceQuery { OccurrenceId = occurrenceId },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetEventOccurrence")
            .WithSummary("One date of a repeating event.");

        group.MapPut("/events/occurrences/{occurrenceId:guid}", async (
                Guid occurrenceId,
                UpdateOccurrenceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateOccurrenceCommand
                        {
                            OccurrenceId = occurrenceId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("UpdateEventOccurrence")
            .WithSummary("Moves or calls off one date, producing an exception on the rule.");
    }
}

/// <summary>The series filters, bound from the query string.</summary>
internal sealed record EventSeriesSearchParameters
{
    [FromQuery(Name = "q")]
    public string? SearchTerm { get; init; }

    [FromQuery(Name = "organizationId")]
    public Guid? OrganizationId { get; init; }

    [FromQuery(Name = "category")]
    public string[]? CategoryKeys { get; init; }

    [FromQuery(Name = "countryCode")]
    public string? CountryCode { get; init; }

    [FromQuery(Name = "city")]
    public string? CityName { get; init; }

    [FromQuery(Name = "status")]
    public string[]? Statuses { get; init; }

    [FromQuery(Name = "mine")]
    public bool? MineOnly { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    public SearchEventSeriesQuery ToQuery() =>
        new()
        {
            SearchTerm = SearchTerm,
            OrganizationId = OrganizationId,
            CategoryKeys = CategoryKeys,
            CountryCode = CountryCode,
            CityName = CityName,
            Statuses = Statuses,
            MineOnly = MineOnly ?? false,
            Cursor = Cursor,
            Limit = Limit
        };
}
