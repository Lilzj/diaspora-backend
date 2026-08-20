using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// What makes one event a date of a repeating one.
/// <para>
/// The occurrence status is the interesting part: it is derived rather than stored, so these tests
/// are what stop the projection and the lifecycle drifting apart. A stored second status would have
/// disagreed with the first the hour the completion sweep moved one and not the other, and nothing
/// would have failed.
/// </para>
/// </summary>
public class EventOccurrenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>An ordinary event belongs to no series and has no occurrence status.</summary>
    [Fact]
    public void A_one_off_event_is_not_an_occurrence()
    {
        var communityEvent = Draft();

        communityEvent.IsOccurrence.Should().BeFalse();
        communityEvent.SeriesId.Should().BeNull();
        communityEvent.SeriesSequence.Should().BeNull();
    }

    /// <summary>Attaching binds the event to a pattern and gives it its ordinal.</summary>
    [Fact]
    public void Attaching_to_a_series_records_the_ordinal()
    {
        var communityEvent = Draft();
        var seriesId = EventSeriesId.New();

        communityEvent.AttachToSeries(seriesId, 7).IsSuccess.Should().BeTrue();

        communityEvent.IsOccurrence.Should().BeTrue();
        communityEvent.SeriesId.Should().Be(seriesId);
        communityEvent.SeriesSequence.Should().Be(7);
    }

    /// <summary>
    /// An event already attached cannot be attached again.
    /// <para>
    /// A pattern must not become the owner of something it did not create — an event that exists on
    /// its own has its own guest list and its own link.
    /// </para>
    /// </summary>
    [Fact]
    public void An_event_cannot_be_attached_to_a_second_series()
    {
        var communityEvent = Draft();
        communityEvent.AttachToSeries(EventSeriesId.New(), 1);

        communityEvent.AttachToSeries(EventSeriesId.New(), 1).IsFailure.Should().BeTrue();
    }

    // ── Moving one date ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Moving one date records the slot the rule generated and raises its own event.</b> The
    /// distinct event is what lets a notification say "this week changed" rather than "the meetup
    /// changed" — the second sentence, said about one date, empties a room for the rest of the year.
    /// </summary>
    [Fact]
    public void Moving_an_occurrence_records_its_generated_slot()
    {
        var communityEvent = Occurrence();
        var generated = communityEvent.Schedule.StartsAt;

        var moved = Schedule(generated.AddDays(1));

        communityEvent.RescheduleOccurrence(moved, null, null, Now).IsSuccess.Should().BeTrue();

        communityEvent.OriginalStartsAt.Should().Be(generated);
        communityEvent.IsSeriesException.Should().BeTrue();
        communityEvent.DomainEvents.OfType<EventOccurrenceRescheduledDomainEvent>()
            .Should().ContainSingle();
    }

    /// <summary>
    /// Moving twice still reports the slot the rule generated, not the previous position.
    /// <para>
    /// The generated slot is the date the series holds an exception for; overwriting it on the
    /// second move would leave the rule generating a date the occurrence no longer sits on.
    /// </para>
    /// </summary>
    [Fact]
    public void Moving_an_occurrence_twice_keeps_the_original_slot()
    {
        var communityEvent = Occurrence();
        var generated = communityEvent.Schedule.StartsAt;

        communityEvent.RescheduleOccurrence(Schedule(generated.AddDays(1)), null, null, Now);
        communityEvent.RescheduleOccurrence(Schedule(generated.AddDays(2)), null, null, Now);

        communityEvent.OriginalStartsAt.Should().Be(generated);
    }

    /// <summary>An event that belongs to no series cannot be moved as an occurrence.</summary>
    [Fact]
    public void A_one_off_event_cannot_be_moved_as_an_occurrence()
    {
        var communityEvent = Draft();

        var moved = communityEvent.RescheduleOccurrence(
            Schedule(communityEvent.Schedule.StartsAt.AddDays(1)), null, null, Now);

        moved.IsFailure.Should().BeTrue();
        moved.Error.Code.Should().Be("community-life.occurrence.not-found");
    }

    /// <summary>A cancelled date cannot be moved. It has no attendees left to move.</summary>
    [Fact]
    public void A_cancelled_occurrence_cannot_be_moved()
    {
        var communityEvent = Occurrence();
        communityEvent.Cancel(Now, null, "Called off");

        var moved = communityEvent.RescheduleOccurrence(
            Schedule(communityEvent.Schedule.StartsAt.AddDays(1)), null, null, Now);

        moved.IsFailure.Should().BeTrue();
        moved.Error.Code.Should().Be("community-life.occurrence.not-movable");
    }

    // ── The derived status ──────────────────────────────────────────────────────────────────

    /// <summary>A date sitting where the rule put it is scheduled.</summary>
    [Fact]
    public void An_unmoved_occurrence_is_scheduled()
    {
        OccurrenceStatusProjection.For(Occurrence()).Should().Be(OccurrenceStatus.Scheduled);
    }

    /// <summary>A moved date is rescheduled, which a client renders differently.</summary>
    [Fact]
    public void A_moved_occurrence_is_rescheduled()
    {
        var communityEvent = Occurrence();

        communityEvent.RescheduleOccurrence(
            Schedule(communityEvent.Schedule.StartsAt.AddDays(1)), null, null, Now);

        OccurrenceStatusProjection.For(communityEvent).Should().Be(OccurrenceStatus.Rescheduled);
    }

    /// <summary>Cancellation wins over having been moved. It is the fact that matters.</summary>
    [Fact]
    public void A_cancelled_occurrence_reports_cancelled_even_after_being_moved()
    {
        var communityEvent = Occurrence();

        communityEvent.RescheduleOccurrence(
            Schedule(communityEvent.Schedule.StartsAt.AddDays(1)), null, null, Now);

        communityEvent.Cancel(Now, null, null);

        OccurrenceStatusProjection.For(communityEvent).Should().Be(OccurrenceStatus.Cancelled);
    }

    /// <summary>
    /// An archived date reports cancelled rather than scheduled: nobody can reach it, and putting it
    /// on a calendar would give a member a link that resolves to nothing.
    /// </summary>
    [Theory]
    [InlineData(EventStatus.Completed, OccurrenceStatus.Completed)]
    [InlineData(EventStatus.Cancelled, OccurrenceStatus.Cancelled)]
    [InlineData(EventStatus.Archived, OccurrenceStatus.Cancelled)]
    [InlineData(EventStatus.Published, OccurrenceStatus.Scheduled)]
    [InlineData(EventStatus.InProgress, OccurrenceStatus.Scheduled)]
    [InlineData(EventStatus.Draft, OccurrenceStatus.Scheduled)]
    public void The_projection_maps_every_lifecycle_state(
        EventStatus status, OccurrenceStatus expected) =>
        OccurrenceStatusProjection.For(status, wasMoved: false).Should().Be(expected);

    /// <summary>A soft-deleted date is not on anybody's calendar.</summary>
    [Fact]
    public void A_deleted_occurrence_reports_cancelled() =>
        OccurrenceStatusProjection
            .For(EventStatus.Published, wasMoved: false, isDeleted: true)
            .Should().Be(OccurrenceStatus.Cancelled);

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static CommunityEvent Occurrence()
    {
        var communityEvent = Draft();
        communityEvent.AttachToSeries(EventSeriesId.New(), 3);

        return communityEvent;
    }

    private static CommunityEvent Draft() =>
        CommunityEvent.Draft(
            CommunityEventId.New(),
            organizationId: null,
            UserId.From(Guid.NewGuid()),
            CommunityLifeTitle.Create("Tuesday Meetup").Value,
            CommunityLifeSlug.FromText("tuesday-meetup"),
            CommunityLifeDescription.Create("A weekly gathering.", null).Value,
            "community-meeting",
            Schedule(Now.AddDays(14)),
            CommunityLifeLocationContext.Create("GB", null, "London").Value,
            EventVenue.Create(name: "Community Hall").Value,
            EventAttendanceMode.InPerson,
            Now).Value;

    private static EventSchedule Schedule(DateTimeOffset startsAt) =>
        EventSchedule.Create(startsAt, startsAt.AddHours(2), "Europe/London").Value;
}
