using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B12.1's recurring events, driven over real HTTP through the same host every other module suite
/// uses.
/// <para>
/// Five claims are under test. That a series <b>expands on the server into real, addressable
/// events</b> — the whole reason the frontend could not build this itself, because occurrence
/// identifiers expanded on a device cannot carry an RSVP. That <b>an occurrence is an event</b>:
/// the identifier the series hands back resolves at <c>/events/{id}</c> and takes a response. That
/// <b>cancelling one date does not cancel the pattern and does not come back</b>. That the
/// <b>three edit scopes do three different things</b>. And that <b>authorization is the server's</b>
/// — a stranger cannot move somebody else's meeting.
/// </para>
/// <para>
/// The wire shapes are re-declared by hand rather than referenced from the module, for the reason
/// the B12 suite records: binding a contract test to the module's own records makes it pass whenever
/// the module compiles.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityLifeSeriesTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/community-life";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityLifeSeriesTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Expansion ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A weekly series writes real dates the moment it is created.</b> Not a rule to be expanded
    /// later on a device — rows, with identifiers, that a reminder can name tomorrow.
    /// </summary>
    [Fact]
    public async Task Creating_a_weekly_series_expands_it_into_real_occurrences()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync();

        series.Status.Should().Be("Draft", "publishing is a separate act");
        series.OccurrenceCount.Should().BeGreaterThan(
            10, "a weekly rule fills a six-month horizon");
        series.Recurrence.Description.Should().NotBeNullOrWhiteSpace();
        series.MaterialisedThrough.Should().NotBeNull();

        var occurrences = await OccurrencesAsync(series.SeriesId, includePast: true);

        occurrences.Items.Should().NotBeEmpty();
        occurrences.Items.Select(item => item.Sequence).Should().BeInAscendingOrder();
        occurrences.Items.Should().OnlyContain(item => item.SeriesId == series.SeriesId);
    }

    /// <summary>
    /// <b>Every occurrence is seven days after the one before it, in local time.</b> The assertion
    /// that would catch a generator adding a fixed number of hours to UTC.
    /// </summary>
    [Fact]
    public async Task Occurrences_are_a_week_apart()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync();
        var occurrences = await OccurrencesAsync(series.SeriesId, includePast: true);

        var starts = occurrences.Items
            .OrderBy(item => item.Sequence)
            .Select(item => item.StartsAt)
            .Take(4)
            .ToList();

        for (var index = 1; index < starts.Count; index++)
        {
            starts[index].Subtract(starts[index - 1]).TotalDays.Should().BeApproximately(7, 0.05);
        }
    }

    /// <summary>
    /// <b>An occurrence identifier is an event identifier.</b> The single most consequential
    /// property of the whole design: everything that already works against an event works against a
    /// date of a series with no second contract.
    /// </summary>
    [Fact]
    public async Task An_occurrence_resolves_as_an_event_and_takes_a_response()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var occurrence = (await OccurrencesAsync(series.SeriesId)).Items[0];

        var (_, attendee) = await RegisterAndVerifyAsync();
        Api.Authenticate(attendee.Tokens.AccessToken);

        var asEvent = await ReadDataAsync<EventDto>(
            await HttpClient.GetAsync($"{Root}/events/{occurrence.OccurrenceId}"));

        asEvent.EventId.Should().Be(occurrence.OccurrenceId);
        asEvent.SeriesId.Should().Be(series.SeriesId);
        asEvent.OccurrenceStatus.Should().Be("Scheduled");

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/events/{occurrence.OccurrenceId}/rsvp",
            new { status = "Going", partySize = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rsvp = await ReadDataAsync<RsvpDto>(response);

        rsvp.Status.Should().Be("Going");
        rsvp.SeriesId.Should().Be(series.SeriesId, "the response knows which pattern it is part of");
    }

    /// <summary>Publishing the series announces the dates already written for it.</summary>
    [Fact]
    public async Task Publishing_a_series_publishes_its_occurrences()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync();

        var published = await ReadDataAsync<SeriesDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/events/series/{series.SeriesId}/publish", new { }));

        published.Status.Should().Be("Published");

        Api.ClearAuthentication();

        var occurrences = await OccurrencesAsync(series.SeriesId);

        occurrences.Items.Should().NotBeEmpty(
            "an anonymous caller sees the announced dates");
        occurrences.Items.Should().OnlyContain(item => item.EventStatus != "Draft");
    }

    /// <summary>
    /// <b>Exactly one date on the page is the next one.</b> Decided by the server so a client never
    /// compares timestamps to reach a rendering decision.
    /// </summary>
    [Fact]
    public async Task Exactly_one_occurrence_is_marked_as_next()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var occurrences = await OccurrencesAsync(series.SeriesId);

        occurrences.Items.Count(item => item.IsNext).Should().Be(1);
        series.NextOccurrence.Should().NotBeNull();
    }

    // ── One date at a time ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Cancelling one date leaves the pattern alone and never regenerates.</b> Without an
    /// exception on the rule, the next expansion writes the date again with a new identifier and
    /// everybody told the meeting was off is silently re-invited to it.
    /// </summary>
    [Fact]
    public async Task Cancelling_one_date_does_not_cancel_the_pattern()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var occurrences = await OccurrencesAsync(series.SeriesId);
        var target = occurrences.Items[1];

        var cancelled = await ReadDataAsync<OccurrenceDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/events/occurrences/{target.OccurrenceId}",
                new { status = "Cancelled", reason = "The hall is double booked" }));

        cancelled.Status.Should().Be("Cancelled");

        var after = await ReadDataAsync<SeriesDto>(
            await HttpClient.GetAsync($"{Root}/events/series/{series.SeriesId}"));

        after.Status.Should().Be("Published", "the pattern is untouched");
        after.CancelledOccurrenceCount.Should().Be(1);
        after.Recurrence.Exceptions.Should().HaveCount(
            1, "the rule now excludes that date, so it is never written again");
    }

    /// <summary>Moving one date records where the rule put it, and excludes that slot.</summary>
    [Fact]
    public async Task Moving_one_date_records_its_original_and_excludes_the_slot()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var target = (await OccurrencesAsync(series.SeriesId)).Items[1];
        var newDate = DateOnly.FromDateTime(target.StartsAt.UtcDateTime.AddDays(1));

        var moved = await ReadDataAsync<OccurrenceDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/events/occurrences/{target.OccurrenceId}",
                new { date = newDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }));

        moved.Status.Should().Be("Rescheduled");
        moved.OriginalStartsAt.Should().Be(target.StartsAt);
        moved.StartsAt.Should().NotBe(target.StartsAt);

        var after = await ReadDataAsync<SeriesDto>(
            await HttpClient.GetAsync($"{Root}/events/series/{series.SeriesId}"));

        after.Recurrence.Exceptions.Should().HaveCount(1);
    }

    /// <summary>A cancelled date cannot be un-cancelled. Running it again is a new date.</summary>
    [Fact]
    public async Task A_cancelled_date_cannot_be_moved_afterwards()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var target = (await OccurrencesAsync(series.SeriesId)).Items[1];

        await HttpClient.PutAsJsonAsync(
            $"{Root}/events/occurrences/{target.OccurrenceId}", new { status = "Cancelled" });

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/events/occurrences/{target.OccurrenceId}",
            new { date = DateOnly.FromDateTime(target.StartsAt.UtcDateTime.AddDays(2))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── The three scopes ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An edit at the series scope moves every date still ahead.</b> The scope is the caller's
    /// decision and the server does exactly what it says.
    /// </summary>
    [Fact]
    public async Task An_entire_series_edit_retitles_every_future_date()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);

        await ReadDataAsync<SeriesDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/events/series/{series.SeriesId}",
                new { scope = "EntireSeries", title = "Renamed Weekly Meetup" }));

        var occurrences = await OccurrencesAsync(series.SeriesId);

        occurrences.Items.Should().OnlyContain(item => item.Title == "Renamed Weekly Meetup");
    }

    /// <summary>
    /// <b>An edit at the occurrence scope moves one date and marks it an exception</b>, so a later
    /// series-wide edit steps over it — the organizer who changed one week meant it.
    /// </summary>
    [Fact]
    public async Task A_single_occurrence_edit_is_not_undone_by_a_later_series_edit()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var target = (await OccurrencesAsync(series.SeriesId)).Items[1];

        await HttpClient.PutAsJsonAsync(
            $"{Root}/events/series/{series.SeriesId}",
            new
            {
                scope = "ThisOccurrence",
                occurrenceId = target.OccurrenceId,
                title = "Special Session"
            });

        await HttpClient.PutAsJsonAsync(
            $"{Root}/events/series/{series.SeriesId}",
            new { scope = "EntireSeries", title = "Renamed Weekly Meetup" });

        var after = await ReadDataAsync<OccurrenceDto>(
            await HttpClient.GetAsync($"{Root}/events/occurrences/{target.OccurrenceId}"));

        after.Title.Should().Be(
            "Special Session", "an individually edited date is the rule's no longer");
    }

    /// <summary>An occurrence-scoped edit with no occurrence named is refused, never widened.</summary>
    [Fact]
    public async Task A_scoped_edit_without_an_occurrence_is_refused()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync();

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/events/series/{series.SeriesId}",
            new { scope = "ThisOccurrence", title = "Nowhere" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>Changing the pattern under the occurrence scope is refused rather than widened.</summary>
    [Fact]
    public async Task Changing_the_rule_at_the_occurrence_scope_is_refused()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var target = (await OccurrencesAsync(series.SeriesId)).Items[0];

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/events/series/{series.SeriesId}",
            new
            {
                scope = "ThisOccurrence",
                occurrenceId = target.OccurrenceId,
                recurrence = new { frequency = "Monthly", interval = 1 }
            });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    // ── Ending the pattern ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Pausing stops generation and cancels nothing.</b> A committee breaking for the summer has
    /// not cancelled September, and telling every attendee otherwise would be the worst possible
    /// reading of the request.
    /// </summary>
    [Fact]
    public async Task Pausing_a_series_leaves_its_announced_dates_standing()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var before = (await OccurrencesAsync(series.SeriesId)).Items.Count;

        var paused = await ReadDataAsync<SeriesDto>(
            await HttpClient.DeleteAsync(
                $"{Root}/events/series/{series.SeriesId}?pauseOnly=true&reason=Summer"));

        paused.Status.Should().Be("Paused");

        var after = await OccurrencesAsync(series.SeriesId);

        after.Items.Should().HaveCount(before);
        after.Items.Should().OnlyContain(item => item.Status != "Cancelled");
    }

    /// <summary>Cancelling the pattern calls off every date still ahead of the clock.</summary>
    [Fact]
    public async Task Cancelling_a_series_cancels_every_future_date()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);

        var cancelled = await ReadDataAsync<SeriesDto>(
            await HttpClient.DeleteAsync(
                $"{Root}/events/series/{series.SeriesId}?reason=The%20group%20wound%20up"));

        cancelled.Status.Should().Be("Cancelled");
        cancelled.CancelledOccurrenceCount.Should().BeGreaterThan(0);

        var occurrences = await OccurrencesAsync(series.SeriesId, includePast: true);

        occurrences.Items.Should().OnlyContain(item => item.Status == "Cancelled");
    }

    // ── Authorization ───────────────────────────────────────────────────────────────────────

    /// <summary>A stranger cannot move somebody else's meeting.</summary>
    [Fact]
    public async Task A_stranger_cannot_edit_a_series()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/events/series/{series.SeriesId}",
            new { scope = "EntireSeries", title = "Hijacked" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    /// <summary>A stranger cannot cancel one of somebody else's dates either.</summary>
    [Fact]
    public async Task A_stranger_cannot_cancel_an_occurrence()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);
        var target = (await OccurrencesAsync(series.SeriesId)).Items[0];

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/events/occurrences/{target.OccurrenceId}",
            new { status = "Cancelled" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    /// <summary>An anonymous caller cannot create a repeating event.</summary>
    [Fact]
    public async Task An_anonymous_caller_cannot_create_a_series()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/events/series", SeriesPayload(publish: false));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Browsing repeating events is anonymous, like every other discovery read.</summary>
    [Fact]
    public async Task An_anonymous_caller_can_browse_series()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/events/series");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A draft series is invisible to everybody but the people who may manage it.</summary>
    [Fact]
    public async Task A_draft_series_is_not_visible_to_a_stranger()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync();

        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/events/series/{series.SeriesId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Refusals ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A rule that never falls on a real date is refused rather than filed. Filed, it would be a
    /// series that never generates anything and never says why.
    /// </summary>
    [Fact]
    public async Task A_rule_that_produces_nothing_is_refused()
    {
        await AuthenticateOrganizerAsync();

        var payload = SeriesPayload(publish: false);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/events/series",
            new
            {
                payload.Title,
                payload.Summary,
                payload.CategoryKey,
                StartDate = "2027-02-01",
                payload.StartTime,
                payload.DurationMinutes,
                payload.TimeZoneId,
                Recurrence = new
                {
                    frequency = "Monthly",
                    interval = 12,
                    byMonthDay = new[] { 31 }
                },
                payload.CountryCode,
                payload.CityName,
                payload.Venue
            });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>An unknown time zone is refused rather than silently treated as UTC.</summary>
    [Fact]
    public async Task An_unknown_time_zone_is_refused()
    {
        await AuthenticateOrganizerAsync();

        var payload = SeriesPayload(publish: false);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/events/series",
            new
            {
                payload.Title,
                payload.Summary,
                payload.CategoryKey,
                payload.StartDate,
                payload.StartTime,
                payload.DurationMinutes,
                TimeZoneId = "Mars/Olympus",
                payload.Recurrence,
                payload.CountryCode,
                payload.CityName,
                payload.Venue
            });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// <b>The client's <c>Custom</c> frequency normalises to weekly and comes back saying so.</b>
    /// The editor sees the rule the server will actually expand, not the word it sent.
    /// </summary>
    [Fact]
    public async Task A_custom_frequency_normalises_to_weekly()
    {
        await AuthenticateOrganizerAsync();

        var payload = SeriesPayload(publish: false);

        var series = await ReadDataAsync<SeriesDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/events/series",
                new
                {
                    payload.Title,
                    payload.Summary,
                    payload.CategoryKey,
                    payload.StartDate,
                    payload.StartTime,
                    payload.DurationMinutes,
                    payload.TimeZoneId,
                    Recurrence = new
                    {
                        frequency = "Custom",
                        interval = 1,
                        byWeekday = new[] { 2, 4 }
                    },
                    payload.CountryCode,
                    payload.CityName,
                    payload.Venue
                }));

        series.Recurrence.Frequency.Should().Be("Weekly");
        series.Recurrence.ByWeekday.Should().Equal(2, 4);
    }

    // ── Paging ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The occurrence page is complete, ordered and never repeats a row.</b> Paged on the
    /// sequence number, which is unique within a series even when two dates have been moved onto the
    /// same evening — the case a timestamp cursor gets wrong.
    /// </summary>
    [Fact]
    public async Task The_occurrence_page_is_complete_and_never_repeats()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);

        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 10; page++)
        {
            var response = await ReadDataAsync<OccurrencePageDto>(
                await HttpClient.GetAsync(
                    $"{Root}/events/series/{series.SeriesId}/occurrences?limit=3"
                    + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}")));

            seen.AddRange(response.Items.Select(item => item.OccurrenceId));
            cursor = response.NextCursor;

            if (!response.HasMore)
            {
                break;
            }
        }

        seen.Should().OnlyHaveUniqueItems("a cursor page must never serve a row twice");
        seen.Should().HaveCountGreaterThan(3, "more than one page was walked");
    }

    /// <summary>A malformed cursor answers the first page rather than a 400.</summary>
    [Fact]
    public async Task A_malformed_cursor_answers_the_first_page()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);

        var response = await HttpClient.GetAsync(
            $"{Root}/events/series/{series.SeriesId}/occurrences?cursor=not-a-cursor");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await ReadDataAsync<OccurrencePageDto>(response);

        page.Items.Should().NotBeEmpty();
    }

    /// <summary>An empty series answers an empty page rather than an error.</summary>
    [Fact]
    public async Task A_window_with_no_dates_answers_an_empty_page()
    {
        await AuthenticateOrganizerAsync();

        var series = await CreateSeriesAsync(publish: true);

        var from = DateTimeOffset.UtcNow.AddYears(5).ToString("O");
        var to = DateTimeOffset.UtcNow.AddYears(6).ToString("O");

        var page = await ReadDataAsync<OccurrencePageDto>(
            await HttpClient.GetAsync(
                $"{Root}/events/series/{series.SeriesId}/occurrences"
                + $"?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}"));

        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    // ── Bounds ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A counted rule writes exactly its count and no more, however far the horizon reaches.
    /// </summary>
    [Fact]
    public async Task A_counted_rule_writes_exactly_its_count()
    {
        await AuthenticateOrganizerAsync();

        var payload = SeriesPayload(publish: false);

        var series = await ReadDataAsync<SeriesDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/events/series",
                new
                {
                    payload.Title,
                    payload.Summary,
                    payload.CategoryKey,
                    payload.StartDate,
                    payload.StartTime,
                    payload.DurationMinutes,
                    payload.TimeZoneId,
                    Recurrence = new
                    {
                        frequency = "Weekly",
                        interval = 1,
                        endKind = "AfterCount",
                        count = 4
                    },
                    payload.CountryCode,
                    payload.CityName,
                    payload.Venue
                }));

        series.OccurrenceCount.Should().Be(4);
        series.Recurrence.Count.Should().Be(4);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task AuthenticateOrganizerAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task<SeriesDto> CreateSeriesAsync(bool publish = false) =>
        await ReadDataAsync<SeriesDto>(
            await HttpClient.PostAsJsonAsync($"{Root}/events/series", SeriesPayload(publish)));

    private async Task<OccurrencePageDto> OccurrencesAsync(
        Guid seriesId, bool includePast = false) =>
        await ReadDataAsync<OccurrencePageDto>(
            await HttpClient.GetAsync(
                $"{Root}/events/series/{seriesId}/occurrences?limit=50&includePast={includePast}"));

    private static SeriesPayloadDto SeriesPayload(bool publish) =>
        new(
            $"Weekly Meetup {Guid.NewGuid():N}",
            "A gathering created by the integration suite.",
            "community-meeting",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "19:00:00",
            120,
            "Europe/London",
            new RecurrencePayloadDto("Weekly", 1),
            "GB",
            "London",
            new VenuePayloadDto("Community Hall", "1 High Street"),
            publish);

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record SeriesPayloadDto(
        string Title,
        string Summary,
        string CategoryKey,
        string StartDate,
        string StartTime,
        int DurationMinutes,
        string TimeZoneId,
        RecurrencePayloadDto Recurrence,
        string CountryCode,
        string CityName,
        VenuePayloadDto Venue,
        bool Publish);

    private sealed record RecurrencePayloadDto(string Frequency, int Interval);

    private sealed record VenuePayloadDto(string Name, string AddressLine1);

    private sealed record SeriesDto(
        Guid SeriesId,
        string Title,
        string Status,
        RecurrenceDto Recurrence,
        int OccurrenceCount,
        int CancelledOccurrenceCount,
        DateOnly? MaterialisedThrough,
        OccurrenceDto? NextOccurrence);

    private sealed record RecurrenceDto(
        string Frequency,
        int Interval,
        List<int> ByWeekday,
        string EndKind,
        int? Count,
        List<DateOnly> Exceptions,
        string Description);

    private sealed record OccurrenceDto(
        Guid OccurrenceId,
        Guid SeriesId,
        int Sequence,
        string Title,
        DateTimeOffset StartsAt,
        string Status,
        string EventStatus,
        DateTimeOffset? OriginalStartsAt,
        bool IsNext);

    private sealed record OccurrencePageDto(
        Guid SeriesId, List<OccurrenceDto> Items, string? NextCursor, bool HasMore);

    private sealed record EventDto(
        Guid EventId, Guid? SeriesId, string? OccurrenceStatus, string Status);

    private sealed record RsvpDto(string Status, int PartySize, Guid? SeriesId);
}
