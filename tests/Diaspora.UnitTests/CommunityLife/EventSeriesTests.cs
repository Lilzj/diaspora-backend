using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// The rules a repeating event is correct at.
/// <para>
/// The lifecycle here has one shape no single event has — <see cref="EventSeriesStatus.Paused"/> —
/// and getting it wrong is expensive in a way that is hard to see: a pause that behaved like a
/// cancellation would tell every attendee of every announced date that the meeting was off, and
/// nothing in the code would look wrong afterwards.
/// </para>
/// </summary>
public class EventSeriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A series always lands as a private draft, whatever the caller asked for.</summary>
    [Fact]
    public void A_new_series_is_a_private_draft()
    {
        var series = Draft();

        series.Status.Should().Be(EventSeriesStatus.Draft);
        series.Visibility.Should().Be(EventVisibility.Private);
        series.PublishedAt.Should().BeNull();
    }

    /// <summary>Creation announces itself, carrying the server's own rendering of the rule.</summary>
    [Fact]
    public void Creating_a_series_raises_its_creation_with_the_rule_in_words()
    {
        var series = Draft();

        var raised = series.DomainEvents.OfType<EventSeriesCreatedDomainEvent>().Single();

        raised.RecurrenceDescription.Should().NotBeNullOrWhiteSpace();
        raised.FirstStartsAt.Should().BeAfter(DateTimeOffset.MinValue);
    }

    /// <summary>
    /// <b>A rule that never falls on a real date is refused at creation.</b> Filed instead, it would
    /// be a series that never generates anything and never says why — indistinguishable from one
    /// whose horizon has not caught up.
    /// </summary>
    [Fact]
    public void A_rule_that_produces_nothing_is_refused()
    {
        var impossible = RecurrenceRule.Create(
            RecurrenceFrequency.Monthly, interval: 12, byMonthDay: [31]).Value;

        var series = Build(impossible, new DateOnly(2027, 2, 1));

        series.IsFailure.Should().BeTrue();
        series.Error.Code.Should().Be("community-life.recurrence.produces-nothing");
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    /// <summary>Publishing makes it public and starts announcing the dates it writes.</summary>
    [Fact]
    public void Publishing_makes_the_series_public_and_its_dates_announced()
    {
        var series = Draft();

        series.Publish(Now).IsSuccess.Should().BeTrue();

        series.Status.Should().Be(EventSeriesStatus.Published);
        series.Visibility.Should().Be(EventVisibility.Public);
        series.PublishesOccurrences.Should().BeTrue();
    }

    /// <summary>
    /// <b>Pausing stops generation and keeps everything already announced.</b> The distinction the
    /// state exists for: a committee breaking for the summer has not cancelled September.
    /// </summary>
    [Fact]
    public void Pausing_stops_generation_without_cancelling_anything()
    {
        var series = Draft();
        series.Publish(Now);

        series.Pause(Now, null, "Summer break").IsSuccess.Should().BeTrue();

        series.Status.Should().Be(EventSeriesStatus.Paused);
        series.GeneratesOccurrences.Should().BeFalse();
        series.CancelledAt.Should().BeNull();
        series.Visibility.Should().Be(
            EventVisibility.Public, "nothing announced has been withdrawn");
    }

    /// <summary>A pause is reversible. That is the whole difference from a cancellation.</summary>
    [Fact]
    public void A_paused_series_can_resume()
    {
        var series = Draft();
        series.Publish(Now);
        series.Pause(Now, null, null);

        series.Resume(Now, null).IsSuccess.Should().BeTrue();

        series.Status.Should().Be(EventSeriesStatus.Published);
        series.GeneratesOccurrences.Should().BeTrue();
    }

    /// <summary>A cancellation is terminal, and nothing brings it back.</summary>
    [Fact]
    public void A_cancelled_series_cannot_be_republished()
    {
        var series = Draft();
        series.Publish(Now);
        series.Cancel(Now, null, "The hall closed");

        series.Status.Should().Be(EventSeriesStatus.Cancelled);
        series.Publish(Now).IsFailure.Should().BeTrue();
        series.Resume(Now, null).IsFailure.Should().BeTrue();
        EventSeriesStatusTransitions.IsTerminal(series.Status).Should().BeTrue();
    }

    /// <summary>A completed series is finished, not merely quiet.</summary>
    [Fact]
    public void A_completed_series_generates_nothing_further()
    {
        var series = Draft();
        series.Publish(Now);
        series.Complete(Now);

        series.Status.Should().Be(EventSeriesStatus.Completed);
        series.GeneratesOccurrences.Should().BeFalse();
    }

    // ── Exceptions ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removing a date twice records it once. Both callers — a cancellation and a move — can reach
    /// the same slot, and a second exception would make the count meaningless.
    /// </summary>
    [Fact]
    public void Removing_the_same_date_twice_records_one_exception()
    {
        var series = Draft();
        var date = new DateOnly(2026, 8, 10);

        series.AddException(date, SeriesExceptionReason.Cancelled, Now);
        series.AddException(date, SeriesExceptionReason.Moved, Now);

        series.Exceptions.Should().ContainSingle();
        series.Exceptions[0].Reason.Should().Be(SeriesExceptionReason.Cancelled);
    }

    /// <summary>A removed date can be put back. The undo for a skip that was a mistake.</summary>
    [Fact]
    public void A_removed_date_can_be_restored()
    {
        var series = Draft();
        var date = new DateOnly(2026, 8, 10);

        series.AddException(date, SeriesExceptionReason.Skipped, Now);
        series.RemoveException(date, Now);

        series.ExceptionDates.Should().NotContain(date);
    }

    // ── Materialisation ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Recording a materialisation does not bump the revision.</b> The sweep extending a horizon
    /// while an organizer edits the series must not make the organizer's save fail — the same
    /// discipline the event aggregate applies to attendance settlement.
    /// </summary>
    [Fact]
    public void Recording_a_materialisation_does_not_move_the_concurrency_token()
    {
        var series = Draft();
        var before = series.Revision;

        series.RecordMaterialisation(12, new DateOnly(2026, 11, 1), written: 12, Now);

        series.Revision.Should().Be(before);
        series.OccurrenceCount.Should().Be(12);
        series.LastMaterialisedSequence.Should().Be(12);
    }

    /// <summary>The horizon never moves backwards, however out of order the passes arrive.</summary>
    [Fact]
    public void The_materialisation_mark_never_moves_backwards()
    {
        var series = Draft();

        series.RecordMaterialisation(12, new DateOnly(2026, 11, 1), 12, Now);
        series.RecordMaterialisation(4, new DateOnly(2026, 9, 1), 0, Now);

        series.MaterialisedThrough.Should().Be(new DateOnly(2026, 11, 1));
        series.LastMaterialisedSequence.Should().Be(12);
    }

    /// <summary>A series at the occurrence ceiling stops generating.</summary>
    [Fact]
    public void A_series_at_the_occurrence_ceiling_stops_generating()
    {
        var series = Draft();
        series.Publish(Now);

        series.RecordMaterialisation(
            CommunityLifeLimits.MaxSeriesOccurrences,
            new DateOnly(2036, 1, 1),
            CommunityLifeLimits.MaxSeriesOccurrences,
            Now);

        series.GeneratesOccurrences.Should().BeFalse();
    }

    // ── Rebasing ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebasing rewinds the horizon to the point the edit takes effect from, so the expansion
    /// rewrites the new rule's dates rather than leaving the old rule's standing.
    /// </summary>
    [Fact]
    public void Rebasing_rewinds_the_horizon_to_the_edit_point()
    {
        var series = Draft();
        series.RecordMaterialisation(20, new DateOnly(2027, 1, 1), 20, Now);

        var monthly = RecurrenceRule.Create(RecurrenceFrequency.Monthly, 1).Value;

        series.Rebase(monthly, null, fromSequence: 5, baseRevision: null)
            .IsSuccess.Should().BeTrue();

        series.LastMaterialisedSequence.Should().Be(4, "the sequence before the first affected one");
        series.MaterialisedThrough.Should().BeNull();
    }

    /// <summary>A rebase that would produce nothing is refused, and the old rule stands.</summary>
    [Fact]
    public void A_rebase_that_would_produce_nothing_is_refused()
    {
        var series = Draft();
        var original = series.Recurrence;

        var impossible = RecurrenceRule.Create(
            RecurrenceFrequency.Yearly,
            interval: 1,
            endKind: RecurrenceEndKind.OnDate,
            until: new DateOnly(2020, 1, 1)).Value;

        series.Rebase(impossible, null, 1, null).IsFailure.Should().BeTrue();
        series.Recurrence.Should().Be(original);
    }

    /// <summary>A rebase to the same rule changes nothing and rewinds nothing.</summary>
    [Fact]
    public void A_rebase_to_the_same_rule_is_a_no_op()
    {
        var series = Draft();
        series.RecordMaterialisation(20, new DateOnly(2027, 1, 1), 20, Now);

        var changed = series.Rebase(series.Recurrence, series.Schedule, 1, null);

        changed.Value.Should().BeEmpty();
        series.LastMaterialisedSequence.Should().Be(
            20, "an edit that changed nothing must not regenerate a hundred dates");
    }

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    /// <summary>An edit against a superseded revision is refused.</summary>
    [Fact]
    public void An_edit_against_a_stale_revision_is_refused()
    {
        var series = Draft();

        series.UpdateContent(baseRevision: null, hostName: "First").IsSuccess.Should().BeTrue();

        var stale = series.UpdateContent(baseRevision: 0, hostName: "Second");

        stale.IsFailure.Should().BeTrue();
        stale.Error.Code.Should().Be("community-life.revision.conflict");
    }

    /// <summary>A terminal series cannot be edited at all.</summary>
    [Fact]
    public void A_cancelled_series_cannot_be_edited()
    {
        var series = Draft();
        series.Publish(Now);
        series.Cancel(Now, null, null);

        series.UpdateContent(baseRevision: null, hostName: "Too late")
            .IsFailure.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static EventSeries Draft() =>
        Build(
            RecurrenceRule.Create(RecurrenceFrequency.Weekly, 1).Value,
            new DateOnly(2026, 8, 3)).Value;

    private static Diaspora.SharedKernel.Results.Result<EventSeries> Build(
        RecurrenceRule rule, DateOnly startDate) =>
        EventSeries.Draft(
            EventSeriesId.New(),
            organizationId: null,
            UserId.From(Guid.NewGuid()),
            CommunityLifeTitle.Create("Tuesday Meetup").Value,
            CommunityLifeSlug.FromText("tuesday-meetup"),
            CommunityLifeDescription.Create("A weekly gathering.", null).Value,
            "community-meeting",
            rule,
            SeriesSchedule.Create(startDate, new TimeOnly(19, 0), 120, "Europe/London").Value,
            CommunityLifeLocationContext.Create("GB", null, "London").Value,
            EventVenue.Create(name: "Community Hall").Value,
            EventAttendanceMode.InPerson,
            Now);
}
