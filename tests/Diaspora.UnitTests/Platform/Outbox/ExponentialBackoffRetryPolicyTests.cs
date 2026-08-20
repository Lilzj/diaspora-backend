using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Retry;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The retry curve, and the promise that it terminates.
/// <para>
/// Retry behaviour is the part of a delivery pipeline nobody can observe in production until it
/// is already wrong: the symptom of a bad curve is either a message given up on too early or a
/// worker hammering a dependency that is trying to recover, and both look like something else
/// from the outside.
/// </para>
/// </summary>
public sealed class ExponentialBackoffRetryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    private static ExponentialBackoffRetryPolicy Policy(Action<OutboxRetryOptions>? configure = null)
    {
        var options = new OutboxOptions();

        // Jitter off by default so the curve itself is assertable. It has its own test.
        options.Retry.BackoffJitter = 0;

        configure?.Invoke(options.Retry);

        return new ExponentialBackoffRetryPolicy(Options.Create(options));
    }

    private static OutboxFailure Failure(int attemptCount, Exception? exception = null) =>
        new(attemptCount, exception ?? new TimeoutException("the consumer was slow"), Now, "test.event");

    /// <summary>
    /// Most publish failures in a modular monolith are a transient blip that is over in
    /// milliseconds. Waiting two seconds to discover that adds latency for nothing.
    /// </summary>
    [Fact]
    public void The_first_failure_retries_immediately()
    {
        var decision = Policy().Decide(Failure(attemptCount: 0));

        decision.Action.Should().Be(OutboxRetryAction.Retry);
        decision.NextAttemptAt.Should().Be(Now);
    }

    [Fact]
    public void The_first_backing_off_attempt_waits_the_initial_delay()
    {
        var decision = Policy().Decide(Failure(attemptCount: 1));

        decision.NextAttemptAt.Should().Be(Now.AddSeconds(2));
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    public void The_delay_doubles_with_each_further_failure(int attemptCount, int expectedSeconds)
    {
        var decision = Policy().Decide(Failure(attemptCount));

        decision.NextAttemptAt.Should().Be(Now.AddSeconds(expectedSeconds));
    }

    /// <summary>
    /// Without a ceiling, attempt fifteen schedules itself for a week from Tuesday — which is a
    /// lost message with none of the visibility a dead letter would have given.
    /// </summary>
    [Fact]
    public void The_delay_is_capped()
    {
        var policy = Policy(retry =>
        {
            retry.MaxAttempts = 40;
            retry.MaxBackoffSeconds = 60;
        });

        policy.DelayFor(attemptsMade: 30).Should().Be(TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// The overflow guard. Doubling unbounded runs a double to infinity and then throws inside
    /// <c>DateTimeOffset</c> arithmetic — in the failure path, the one place where an exception
    /// turns a retry into a lost message.
    /// </summary>
    [Fact]
    public void A_very_large_attempt_count_does_not_overflow_the_delay()
    {
        var policy = Policy(retry =>
        {
            retry.MaxAttempts = 100;
            retry.MaxBackoffSeconds = 86400;
        });

        var act = () => policy.DelayFor(attemptsMade: 99);

        act.Should().NotThrow();
        policy.DelayFor(attemptsMade: 99).Should().Be(TimeSpan.FromSeconds(86400));
    }

    /// <summary>
    /// An outage fails every in-flight message at the same instant. Without jitter they all
    /// come back in one synchronised wave, which is how a recovering dependency is knocked over
    /// by its own backlog.
    /// </summary>
    [Fact]
    public void Jitter_lengthens_the_delay_and_never_shortens_it()
    {
        var policy = Policy(retry => retry.BackoffJitter = 0.25);

        var delays = Enumerable.Range(0, 200)
            .Select(_ => policy.DelayFor(attemptsMade: 3).TotalSeconds)
            .ToArray();

        // Three attempts made, one of them immediate, so this is the second backing-off step:
        // 2s doubled once, i.e. 4s unjittered, and at most 4s + 25%.
        delays.Should().OnlyContain(seconds => seconds >= 4);
        delays.Should().OnlyContain(seconds => seconds <= 5);

        // And it actually varies — a jitter setting that produced a constant would pass every
        // bounds check and provide none of the protection.
        delays.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void The_budget_is_finite()
    {
        var policy = Policy(retry => retry.MaxAttempts = 3);

        // Attempts 0 and 1 retry; the third failure exhausts the budget.
        policy.Decide(Failure(0)).Action.Should().Be(OutboxRetryAction.Retry);
        policy.Decide(Failure(1)).Action.Should().Be(OutboxRetryAction.Retry);

        var final = policy.Decide(Failure(2));

        final.Action.Should().Be(OutboxRetryAction.DeadLetter);
        final.Reason.Should().Be(OutboxDeadLetterReason.MaxAttemptsExceeded);
    }

    /// <summary>
    /// An unreadable payload is decided entirely by bytes already on disk. Spending eight
    /// attempts over forty minutes proving that only delays the dead letter an operator needs.
    /// </summary>
    [Fact]
    public void An_unreadable_payload_dead_letters_on_the_first_failure()
    {
        var decision = Policy().Decide(
            Failure(0, new OutboxSerializationException("the payload is not readable")));

        decision.Action.Should().Be(OutboxRetryAction.DeadLetter);
        decision.Reason.Should().Be(OutboxDeadLetterReason.SerializationFailure);
    }

    /// <summary>
    /// Distinguished from a serialization failure because the remedy differs: this message is
    /// fine and becomes deliverable the moment the right assembly ships.
    /// </summary>
    [Fact]
    public void An_unknown_contract_dead_letters_with_its_own_reason()
    {
        var decision = Policy().Decide(
            Failure(0, new UnknownIntegrationEventException("future.contract", 2)));

        decision.Action.Should().Be(OutboxRetryAction.DeadLetter);
        decision.Reason.Should().Be(OutboxDeadLetterReason.UnknownEventType);
    }

    /// <summary>
    /// A transport almost always wraps what it caught, and a fatal failure three frames down is
    /// no less fatal for it.
    /// </summary>
    [Fact]
    public void A_fatal_failure_is_recognised_through_a_wrapping_exception()
    {
        var wrapped = new InvalidOperationException(
            "publishing failed",
            new AggregateException(new OutboxSerializationException("unreadable")));

        Policy().Decide(Failure(0, wrapped)).Action.Should().Be(OutboxRetryAction.DeadLetter);
    }

    [Fact]
    public void A_deployment_can_declare_its_own_poison_exception_types()
    {
        var policy = Policy(retry => retry.PoisonExceptionTypes.Add(nameof(NotSupportedException)));

        var decision = policy.Decide(Failure(0, new NotSupportedException("this consumer never will")));

        decision.Action.Should().Be(OutboxRetryAction.DeadLetter);
        decision.Reason.Should().Be(OutboxDeadLetterReason.Poison);
    }

    /// <summary>
    /// The property that matters most: from any starting attempt, the policy reaches a verdict.
    /// There is no input for which it says "try again" forever.
    /// </summary>
    [Fact]
    public void The_policy_always_terminates()
    {
        var policy = Policy(retry => retry.MaxAttempts = 5);

        var attempts = 0;
        var decision = policy.Decide(Failure(attempts));

        while (decision.Action == OutboxRetryAction.Retry && attempts < 1000)
        {
            decision = policy.Decide(Failure(++attempts));
        }

        decision.Action.Should().Be(OutboxRetryAction.DeadLetter);
        attempts.Should().BeLessThan(5);
    }
}
