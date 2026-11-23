using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CL = Diaspora.Modules.CommunityLife.Domain;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// RMD-008 — the community calendar, over real HTTP against real rows.
/// <para>
/// <b>Nothing asked this route anything before 23 November 2026.</b> The client read its answer as
/// a bare list and threw spreading it, so the calendar tab never showed a platform entry, and no
/// test here called it either. These pin what a client needs to put an entry on the right day at
/// the right time, and to open the event it belongs to.
/// </para>
/// <para>
/// <b>An entry names the zone its times are local to.</b> An instant alone cannot say that a Lagos
/// event is at 7pm to a member reading it in London, or which day an all-day entry falls on there.
/// The events are seeded in Lagos, which is not UTC, so a zone dropped on the way out cannot pass
/// by coincidence.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class CommunityCalendarTests : PooledIdentityTestBase
{
    private const string Lagos = "Africa/Lagos";

    public CommunityCalendarTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_entry_names_the_zone_its_times_are_local_to_and_the_event_it_is()
    {
        // 18:00 UTC is 19:00 in Lagos. Ten days out, inside any window the route allows.
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(10), TimeSpan.Zero).AddHours(18);
        var run = await SeedAsync(start);

        Api.ClearAuthentication();
        var entries = await EntriesAsync(start.AddDays(-1), start.AddDays(3), run.Tag);

        entries.Keys.Should().BeEquivalentTo(
            new[] { run.TimedId, run.AllDayId }, "both seeded events are in the window and in Nigeria");

        var timed = entries[run.TimedId];
        timed.GetProperty("timeZoneId").GetString().Should().Be(Lagos);
        timed.GetProperty("startsAt").GetDateTimeOffset().Should().Be(start);
        timed.GetProperty("isAllDay").GetBoolean().Should().BeFalse();
        timed.GetProperty("sourceKey").GetString().Should().Be("community-events");
        timed.GetProperty("entryType").GetString().Should().Be(
            "community-meeting", "an event's entry type is its category key, never the word Event");

        var allDay = entries[run.AllDayId];
        allDay.GetProperty("timeZoneId").GetString().Should().Be(Lagos);
        allDay.GetProperty("isAllDay").GetBoolean().Should().BeTrue("it runs for a whole day");
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────

    private sealed record SeededRun(string Tag, Guid TimedId, Guid AllDayId);

    /// <summary>The run's own entries, by the event they are. The store is shared.</summary>
    private async Task<Dictionary<Guid, JsonElement>> EntriesAsync(
        DateTimeOffset from, DateTimeOffset to, string tag)
    {
        var response = await HttpClient.GetAsync(
            "/api/v1/community-life/calendar?countryCode=NG"
            + $"&from={Uri.EscapeDataString(from.ToString("O"))}"
            + $"&to={Uri.EscapeDataString(to.ToString("O"))}");

        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(
            "the server answered {0}: {1}", response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");

        // The page is an object around the entries. Read as a bare list, this is what threw.
        data.ValueKind.Should().Be(JsonValueKind.Object);

        return data.GetProperty("entries").EnumerateArray()
            .Where(entry => entry.GetProperty("title").GetString()!.EndsWith(tag, StringComparison.Ordinal))
            .ToDictionary(entry => entry.GetProperty("eventId").GetGuid(), entry => entry.Clone());
    }

    private async Task<SeededRun> SeedAsync(DateTimeOffset start)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var tag = Guid.NewGuid().ToString("N")[..8];
        var organizerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        CL.CommunityEvents.CommunityEvent Event(string label, TimeSpan length)
        {
            var communityEvent = CL.CommunityEvents.CommunityEvent.Draft(
                CL.Identifiers.CommunityEventId.New(),
                organizationId: null,
                organizerId,
                CL.ValueObjects.CommunityLifeTitle.Create($"{label} {tag}").Value,
                CL.ValueObjects.CommunityLifeSlug.FromText($"calendar-{tag}-{label}"),
                CL.ValueObjects.CommunityLifeDescription.Create(
                    $"A seeded event for the calendar suite: {label}.", null).Value,
                "community-meeting",
                CL.ValueObjects.EventSchedule.Create(start, start.Add(length), Lagos).Value,
                CL.ValueObjects.CommunityLifeLocationContext.Create("NG", "Lagos", "Lagos").Value,
                CL.ValueObjects.EventVenue.Create(
                    name: "Community Hall", latitude: 6.4541, longitude: 3.3947).Value,
                CL.Enums.EventAttendanceMode.InPerson,
                now).Value;

            // Asserted rather than ignored: an unpublished seed is absent from every calendar, and
            // "nothing came back" would then read as a failure of the thing under test.
            communityEvent.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<CL.CommunityEvents.CommunityEvent>().Add(communityEvent);
            return communityEvent;
        }

        var timed = Event("evening-meeting", TimeSpan.FromHours(2));
        var allDay = Event("all-day-festival", TimeSpan.FromHours(24));

        await dbContext.SaveChangesAsync();

        return new SeededRun(tag, timed.Id.Value, allDay.Id.Value);
    }
}
