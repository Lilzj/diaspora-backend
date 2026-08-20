using Diaspora.Operations.Scheduling;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The cron parser, which is small enough to be worth reading and subtle enough to be worth testing.
/// <para>
/// The day rule in particular: when both day fields are restricted, cron matches if <em>either</em>
/// does. Getting that backwards makes such expressions fire almost never instead of often, and it is
/// the single most common bug in a hand-written cron evaluator.
/// </para>
/// </summary>
public sealed class CronExpressionTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("* * * * *", "2026-03-15T10:31:00Z")]
    [InlineData("0 * * * *", "2026-03-15T11:00:00Z")]
    [InlineData("0 0 * * *", "2026-03-16T00:00:00Z")]
    [InlineData("30 2 * * *", "2026-03-16T02:30:00Z")]
    [InlineData("*/15 * * * *", "2026-03-15T10:45:00Z")]
    [InlineData("0 0 1 * *", "2026-04-01T00:00:00Z")]
    [InlineData("0 0 1 1 *", "2027-01-01T00:00:00Z")]
    public void The_next_occurrence_is_computed(string expression, string expected)
    {
        CronExpression.Parse(expression)
            .GetNextOccurrence(Reference)
            .Should().Be(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("@hourly", "2026-03-15T11:00:00Z")]
    [InlineData("@daily", "2026-03-16T00:00:00Z")]
    [InlineData("@monthly", "2026-04-01T00:00:00Z")]
    public void The_standard_aliases_are_understood(string expression, string expected)
    {
        CronExpression.Parse(expression)
            .GetNextOccurrence(Reference)
            .Should().Be(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void A_named_month_and_day_are_understood()
    {
        CronExpression.Parse("0 0 * jun mon")
            .GetNextOccurrence(Reference)
            .Should().Be(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                "the first of June 2026 is a Monday");
    }

    [Fact]
    public void When_both_day_fields_are_restricted_either_one_matches()
    {
        // 15 March 2026 is a Sunday. "0 0 1 * 1" means the first of the month *and* every Monday,
        // so the next occurrence is Monday the 16th — not the first of April.
        CronExpression.Parse("0 0 1 * 1")
            .GetNextOccurrence(Reference)
            .Should().Be(new DateTimeOffset(2026, 3, 16, 0, 0, 0, TimeSpan.Zero),
                "cron's day rule is an OR when both fields are restricted, which surprises everybody " +
                "and which every implementation since the original crontab has copied");
    }

    [Fact]
    public void Sunday_is_accepted_as_both_zero_and_seven()
    {
        var asZero = CronExpression.Parse("0 0 * * 0").GetNextOccurrence(Reference);
        var asSeven = CronExpression.Parse("0 0 * * 7").GetNextOccurrence(Reference);

        asZero.Should().Be(asSeven);
    }

    [Fact]
    public void An_expression_that_can_never_match_returns_nothing_rather_than_hanging()
    {
        CronExpression.Parse("0 0 30 2 *")
            .GetNextOccurrence(Reference)
            .Should().BeNull(
                "the thirtieth of February matches nothing ever, and an unbounded search for it " +
                "would hang a worker rather than report a bad schedule");
    }

    [Theory]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("99 * * * *")]
    [InlineData("* * * notamonth *")]
    [InlineData("10-5 * * * *")]
    [InlineData("*/0 * * * *")]
    public void A_malformed_expression_is_refused_at_parse_time(string expression)
    {
        CronExpression.TryParse(expression, out _).Should().BeFalse(
            "a bad schedule should fail the startup that registered it, not silently never fire");
    }

    [Fact]
    public void The_next_occurrence_is_strictly_after_the_moment_given()
    {
        var exactly = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

        CronExpression.Parse("0 * * * *")
            .GetNextOccurrence(exactly)
            .Should().Be(new DateTimeOffset(2026, 3, 15, 11, 0, 0, TimeSpan.Zero),
                "calling this with a job's last run must never return that same run");
    }
}

/// <summary>The in-memory scheduler's claim, completion and recurrence behaviour.</summary>
public sealed class InMemoryJobSchedulerTests
{
    [Fact]
    public async Task An_enqueued_job_is_pending_and_findable()
    {
        var scheduler = Build(out _);

        var id = await scheduler.EnqueueAsync("test.job", "{}");

        var job = await scheduler.FindAsync(id);

        job.Should().NotBeNull();
        job!.Status.Should().Be(JobStatus.Pending);
        job.JobType.Should().Be("test.job");
        job.Payload.Should().Be("{}");
    }

    [Fact]
    public async Task A_delayed_job_is_not_claimable_until_its_moment()
    {
        var scheduler = Build(out var clock);

        await scheduler.ScheduleAsync("test.job", TimeSpan.FromMinutes(5));

        scheduler.Claim().Should().BeNull();

        clock.Advance(TimeSpan.FromMinutes(6));

        scheduler.Claim().Should().NotBeNull();
    }

    [Fact]
    public async Task Higher_priority_is_claimed_first()
    {
        var scheduler = Build(out _);

        await scheduler.EnqueueAsync("low", priority: JobPriority.Low);
        await scheduler.EnqueueAsync("high", priority: JobPriority.High);

        scheduler.Claim()!.JobType.Should().Be("high");
    }

    [Fact]
    public async Task A_claimed_job_cannot_be_claimed_again()
    {
        var scheduler = Build(out _);

        await scheduler.EnqueueAsync("test.job");

        scheduler.Claim().Should().NotBeNull();
        scheduler.Claim().Should().BeNull();
    }

    [Fact]
    public async Task A_failed_job_goes_back_to_pending_with_a_delay()
    {
        var scheduler = Build(out var clock);

        await scheduler.EnqueueAsync("test.job");

        var job = scheduler.Claim()!;
        scheduler.Complete(job, new InvalidOperationException("failed"), poisonThreshold: 5);

        (await scheduler.FindAsync(job.Id))!.Status.Should().Be(JobStatus.Pending);

        scheduler.Claim().Should().BeNull("a failing job must not spin");

        clock.Advance(TimeSpan.FromSeconds(10));

        scheduler.Claim().Should().NotBeNull();
    }

    [Fact]
    public async Task A_job_that_reaches_the_poison_threshold_is_dead_lettered()
    {
        var scheduler = Build(out var clock);

        await scheduler.EnqueueAsync("test.job");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var claimed = scheduler.Claim();
            claimed.Should().NotBeNull();
            scheduler.Complete(claimed!, new InvalidOperationException("failed"), poisonThreshold: 3);
            clock.Advance(TimeSpan.FromMinutes(10));
        }

        var jobs = await scheduler.ListAsync(JobStatus.DeadLettered);

        jobs.Should().ContainSingle(
            "retrying one bad message forever is how a single job consumes a whole consumer");
    }

    [Fact]
    public async Task A_cancelled_job_is_never_claimed()
    {
        var scheduler = Build(out _);

        var id = await scheduler.EnqueueAsync("test.job");

        (await scheduler.CancelAsync(id)).Should().BeTrue();

        scheduler.Claim().Should().BeNull();
    }

    [Fact]
    public async Task Cancelling_a_finished_job_reports_that_it_did_nothing()
    {
        var scheduler = Build(out _);

        await scheduler.EnqueueAsync("test.job");

        var job = scheduler.Claim()!;
        scheduler.Complete(job, failure: null, poisonThreshold: 5);

        (await scheduler.CancelAsync(job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task A_recurring_registration_promotes_a_job_when_its_moment_arrives()
    {
        var scheduler = Build(out var clock);

        await scheduler.AddRecurringAsync("nightly", "test.job", "0 0 * * *");

        scheduler.PromoteDueRecurring().Should().Be(0);

        clock.Advance(TimeSpan.FromDays(1));

        scheduler.PromoteDueRecurring().Should().Be(1);
        scheduler.Claim().Should().NotBeNull();
    }

    [Fact]
    public async Task Re_registering_a_recurring_job_replaces_it_rather_than_duplicating_it()
    {
        var scheduler = Build(out _);

        await scheduler.AddRecurringAsync("nightly", "test.job", "0 0 * * *");
        await scheduler.AddRecurringAsync("nightly", "test.job", "0 3 * * *");

        scheduler.Recurring.Should().ContainSingle(
            "every replica registers the same recurring jobs at every startup, so registration has " +
            "to be idempotent");
    }

    [Fact]
    public async Task A_recurring_registration_with_a_bad_expression_is_refused_immediately()
    {
        var scheduler = Build(out _);

        var register = async () =>
            await scheduler.AddRecurringAsync("bad", "test.job", "not a cron expression");

        await register.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task A_finished_job_is_pruned_and_a_dead_lettered_one_is_kept()
    {
        var scheduler = Build(out var clock);

        await scheduler.EnqueueAsync("succeeds");
        var succeeded = scheduler.Claim()!;
        scheduler.Complete(succeeded, failure: null, poisonThreshold: 5);

        await scheduler.EnqueueAsync("dies");
        var died = scheduler.Claim()!;
        scheduler.Complete(died, new InvalidOperationException("failed"), poisonThreshold: 1);

        clock.Advance(TimeSpan.FromDays(1));

        scheduler.Prune(TimeSpan.FromHours(6)).Should().Be(1);

        (await scheduler.FindAsync(died.Id)).Should().NotBeNull(
            "dead letters are the ones an operator needs to look at; a retention sweep that " +
            "discarded them would delete the evidence of the only failures anybody cares about");
    }

    private static InMemoryJobScheduler Build(out FakeClock clock)
    {
        clock = new FakeClock();

        return new InMemoryJobScheduler(
            clock, new FakeGuidGenerator(), NullLogger<InMemoryJobScheduler>.Instance);
    }
}

/// <summary>The in-process lease, and what it does and does not promise.</summary>
public sealed class InProcessDistributedLockTests
{
    [Fact]
    public async Task A_lease_is_granted_once_and_refused_while_held()
    {
        var clock = new FakeClock();
        var locks = new InProcessDistributedLock(clock);

        await using var held = await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(5));

        held.Should().NotBeNull();

        (await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(5))).Should().BeNull();
    }

    [Fact]
    public async Task A_released_lease_can_be_taken_again()
    {
        var locks = new InProcessDistributedLock(new FakeClock());

        var first = await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(5));
        await first!.DisposeAsync();

        (await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(5))).Should().NotBeNull();
    }

    [Fact]
    public async Task An_expired_lease_is_taken_over()
    {
        var clock = new FakeClock();
        var locks = new InProcessDistributedLock(clock);

        await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromMinutes(2));

        (await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(1))).Should().NotBeNull(
            "a lock held by a process that has been killed must expire on its own, or the resource " +
            "is locked until a human intervenes");
    }

    [Fact]
    public async Task Renewal_fails_once_the_lease_has_been_taken_over()
    {
        var clock = new FakeClock();
        var locks = new InProcessDistributedLock(clock);

        var first = await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromMinutes(2));

        await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));

        (await first!.RenewAsync(TimeSpan.FromMinutes(1))).Should().BeFalse(
            "a holder that renewed after losing its lease would keep working alongside whoever took " +
            "over, which is the exact condition a lock exists to prevent");
    }

    [Fact]
    public async Task Renewal_extends_a_lease_that_is_still_held()
    {
        var clock = new FakeClock();
        var locks = new InProcessDistributedLock(clock);

        await using var held = await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromSeconds(30));

        (await held!.RenewAsync(TimeSpan.FromMinutes(5))).Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(2));

        (await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(1))).Should().BeNull();
    }

    [Fact]
    public async Task The_holder_is_reported_while_the_lease_is_live()
    {
        var locks = new InProcessDistributedLock(new FakeClock());

        await using var held = await locks.TryAcquireAsync("resource", TimeSpan.FromMinutes(5));

        (await locks.FindHolderAsync("resource")).Should().Be(held!.Owner);
        (await locks.FindHolderAsync("other")).Should().BeNull();
    }
}
