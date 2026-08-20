using Diaspora.Operations.Resilience;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The circuit breaker's state machine, driven by a clock the test controls.
/// <para>
/// A breaker is one of the few components where every interesting behaviour is a function of time, and
/// testing it against the wall clock would mean either sleeping — making the suite slow and flaky — or
/// asserting nothing about the parts that matter.
/// </para>
/// </summary>
public sealed class CircuitBreakerTests
{
    [Fact]
    public void A_closed_circuit_admits_calls()
    {
        var breaker = Build(out _);

        breaker.TryEnter(out _, out var trial).Should().BeTrue();
        trial.Should().BeFalse();
    }

    [Fact]
    public void Failures_below_the_minimum_throughput_do_not_open_the_circuit()
    {
        var breaker = Build(out _, policy => policy with { MinimumThroughput = 10, FailureRatio = 0.5 });

        for (var attempt = 0; attempt < 9; attempt++)
        {
            breaker.TryEnter(out _, out _);
            breaker.OnFailure().Should().BeFalse();
        }

        breaker.Snapshot().State.Should().Be(CircuitState.Closed,
            "a ratio computed over three calls says nothing about a dependency's health, which is " +
            "why a quiet endpoint must not be able to trip a breaker");
    }

    [Fact]
    public void Crossing_the_ratio_with_enough_throughput_opens_the_circuit()
    {
        var breaker = Build(out _, policy => policy with { MinimumThroughput = 4, FailureRatio = 0.5 });

        breaker.OnSuccess();
        breaker.OnSuccess();
        breaker.OnFailure();

        breaker.OnFailure().Should().BeTrue();
        breaker.Snapshot().State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void An_open_circuit_refuses_calls_until_the_break_elapses()
    {
        var breaker = Open(out var clock);

        breaker.TryEnter(out var closesAt, out _).Should().BeFalse();
        closesAt.Should().BeAfter(clock.UtcNow);

        clock.Advance(TimeSpan.FromSeconds(31));

        breaker.TryEnter(out _, out var trial).Should().BeTrue();
        trial.Should().BeTrue("the first call after a break is a trial, not a return to normal");
    }

    [Fact]
    public void Only_one_trial_call_is_admitted_while_half_open()
    {
        var breaker = Open(out var clock);

        clock.Advance(TimeSpan.FromSeconds(31));

        breaker.TryEnter(out _, out _).Should().BeTrue();
        breaker.TryEnter(out _, out _).Should().BeFalse(
            "a dependency that has just failed should not receive a burst the instant it looks alive");
    }

    [Fact]
    public void A_successful_trial_closes_the_circuit_and_clears_the_window()
    {
        var breaker = Open(out var clock);

        clock.Advance(TimeSpan.FromSeconds(31));
        breaker.TryEnter(out _, out _);
        breaker.OnSuccess();

        var snapshot = breaker.Snapshot();

        snapshot.State.Should().Be(CircuitState.Closed);
        snapshot.FailureRatio.Should().Be(0,
            "keeping the failures that opened the circuit would reopen it on the very next failure, " +
            "however much traffic had succeeded in between");
    }

    [Fact]
    public void A_failed_trial_reopens_the_circuit_for_a_full_break()
    {
        var breaker = Open(out var clock);

        clock.Advance(TimeSpan.FromSeconds(31));
        breaker.TryEnter(out _, out _);
        breaker.OnFailure().Should().BeTrue();

        breaker.Snapshot().State.Should().Be(CircuitState.Open);
        breaker.TryEnter(out _, out _).Should().BeFalse();
    }

    [Fact]
    public void An_isolated_circuit_never_closes_on_its_own()
    {
        var breaker = Build(out var clock);

        breaker.Isolate();
        clock.Advance(TimeSpan.FromHours(6));

        breaker.TryEnter(out _, out _).Should().BeFalse();
        breaker.Snapshot().State.Should().Be(CircuitState.Isolated,
            "a manual isolation that silently healed would be worse than none: an operator who " +
            "isolated a provider for a maintenance window would have it quietly reopen mid-window");

        breaker.Reset();

        breaker.TryEnter(out _, out _).Should().BeTrue();
    }

    [Fact]
    public void The_sampling_window_ages_out_old_failures()
    {
        var breaker = Build(out var clock, policy => policy with
        {
            MinimumThroughput = 4,
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30)
        });

        breaker.OnFailure();
        breaker.OnFailure();
        breaker.OnFailure();

        clock.Advance(TimeSpan.FromSeconds(31));

        breaker.Snapshot().Throughput.Should().Be(0,
            "counts from before a quiet period say nothing about a dependency's health now");
    }

    [Fact]
    public void Trip_count_survives_the_circuit_closing()
    {
        var breaker = Open(out var clock);

        clock.Advance(TimeSpan.FromSeconds(31));
        breaker.TryEnter(out _, out _);
        breaker.OnSuccess();

        breaker.Snapshot().TotalTrips.Should().Be(1,
            "a breaker that has opened and recovered five times today is a finding, and a state " +
            "snapshot alone would show it as closed and healthy");
    }

    private static CircuitBreaker Open(out FakeClock clock)
    {
        var breaker = Build(out clock, policy => policy with
        {
            MinimumThroughput = 2,
            FailureRatio = 0.5,
            BreakDuration = TimeSpan.FromSeconds(30)
        });

        breaker.OnFailure();
        breaker.OnFailure();

        breaker.Snapshot().State.Should().Be(CircuitState.Open);

        return breaker;
    }

    private static CircuitBreaker Build(
        out FakeClock clock, Func<CircuitBreakerPolicy, CircuitBreakerPolicy>? tune = null)
    {
        clock = new FakeClock();

        var policy = new CircuitBreakerPolicy("test")
        {
            MinimumThroughput = 2,
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30)
        };

        return new CircuitBreaker("test", tune is null ? policy : tune(policy), clock);
    }
}

/// <summary>The executor's composition of bulkhead, breaker, retry and timeout.</summary>
public sealed class ResilienceExecutorTests
{
    [Fact]
    public async Task A_transient_failure_is_retried_and_the_result_returned()
    {
        var executor = Build();
        var attempts = 0;

        var result = await executor.ExecuteAsync("test.dependency", _ =>
        {
            attempts++;
            return attempts < 3
                ? throw new TimeoutException("still warming up")
                : Task.FromResult("ok");
        });

        result.Should().Be("ok");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task A_permanent_failure_is_not_retried()
    {
        var executor = Build();
        var attempts = 0;

        var call = async () => await executor.ExecuteAsync<string>("test.dependency", _ =>
        {
            attempts++;
            throw new ArgumentException("this input will never work");
        });

        await call.Should().ThrowAsync<ArgumentException>();

        attempts.Should().Be(1,
            "retrying a permanent failure wastes time against a dependency that is working fine, " +
            "and on a non-idempotent operation it does worse than waste time");
    }

    [Fact]
    public async Task Cancellation_is_never_treated_as_a_failure()
    {
        var executor = Build();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var call = async () => await executor.ExecuteAsync<string>(
            "test.dependency",
            _ => throw new OperationCanceledException(source.Token),
            source.Token);

        await call.Should().ThrowAsync<OperationCanceledException>();

        ((ICircuitBreakerRegistry)executor).Find("test.dependency")!.FailureRatio.Should().Be(0,
            "a member closing an app must not contribute to a breaker's failure ratio");
    }

    [Fact]
    public async Task An_open_circuit_refuses_without_calling_the_dependency()
    {
        var executor = Build(breaker => breaker with { MinimumThroughput = 1, FailureRatio = 0.5 });
        var calls = 0;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                await executor.ExecuteAsync<string>("test.dependency", _ =>
                {
                    calls++;
                    throw new TimeoutException();
                });
            }
            catch (Exception exception) when (exception is TimeoutException or CircuitOpenException)
            {
                // Both are expected: the first sequence exhausts its retries, and once the circuit is
                // open every later attempt is refused before the dependency is reached.
            }
        }

        var before = calls;

        var refused = async () => await executor.ExecuteAsync<string>(
            "test.dependency", _ => { calls++; return Task.FromResult("never"); });

        await refused.Should().ThrowAsync<CircuitOpenException>();

        calls.Should().Be(before, "an open circuit means the dependency was not called at all");
    }

    [Fact]
    public async Task Retries_stop_when_the_overall_timeout_would_be_exceeded()
    {
        var clock = new FakeClock();

        var executor = Build(
            retry: policy => policy with
            {
                MaxAttempts = 10,

                // The first proposed delay is longer than the whole budget, so the deadline is
                // already crossed before the second attempt could start. Making the delay and the
                // budget equal would not test this: the check is a strict comparison, and equal
                // means the attempt still fits.
                InitialDelay = TimeSpan.FromMilliseconds(500),
                MaxDelay = TimeSpan.FromSeconds(1),
                JitterRatio = 0,
                OverallTimeout = TimeSpan.FromMilliseconds(10)
            },
            clock: clock);

        var attempts = 0;

        var call = async () => await executor.ExecuteAsync<string>("test.dependency", _ =>
        {
            attempts++;
            throw new TimeoutException();
        });

        await call.Should().ThrowAsync<TimeoutException>();

        // The clock never advances, so the deadline is computed from a fixed "now" and the very first
        // proposed delay already crosses it. One attempt, no retries: a caller behind a gateway
        // timeout gains nothing from a fourth attempt after the gateway has given up.
        attempts.Should().Be(1);
    }

    private static ResilienceExecutor Build(
        Func<CircuitBreakerPolicy, CircuitBreakerPolicy>? breaker = null,
        Func<RetryPolicy, RetryPolicy>? retry = null,
        FakeClock? clock = null)
    {
        var retryPolicy = new RetryPolicy("test.dependency")
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(2),
            JitterRatio = 0
        };

        var breakerPolicy = new CircuitBreakerPolicy("test.dependency")
        {
            MinimumThroughput = 100,
            FailureRatio = 0.99,
            BreakDuration = TimeSpan.FromMinutes(1),
            CallTimeout = null
        };

        var catalog = new OperationalPolicyCatalog(
        [
            new Contributor(
                retry is null ? retryPolicy : retry(retryPolicy),
                breaker is null ? breakerPolicy : breaker(breakerPolicy))
        ]);

        return new ResilienceExecutor(
            catalog,
            [new DefaultFailureClassifier()],
            clock ?? new FakeClock(),
            NullLogger<ResilienceExecutor>.Instance);
    }

    private sealed class Contributor : IOperationalPolicyContributor
    {
        private readonly OperationalPolicy[] _policies;

        public Contributor(params OperationalPolicy[] policies) => _policies = policies;

        public string Module => "Test";

        public IEnumerable<OperationalPolicy> Policies => _policies;
    }
}

/// <summary>The default classifier's view of what is worth retrying.</summary>
public sealed class FailureClassifierTests
{
    [Theory]
    [InlineData(typeof(TimeoutException), FailureClassification.Transient)]
    [InlineData(typeof(IOException), FailureClassification.Transient)]
    [InlineData(typeof(ArgumentException), FailureClassification.Permanent)]
    [InlineData(typeof(InvalidOperationException), FailureClassification.Permanent)]
    [InlineData(typeof(NotSupportedException), FailureClassification.Permanent)]
    public void Known_shapes_are_classified(Type exceptionType, FailureClassification expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        new DefaultFailureClassifier().Classify(exception).Should().Be(expected);
    }

    [Fact]
    public void Cancellation_is_its_own_classification()
    {
        new DefaultFailureClassifier()
            .Classify(new OperationCanceledException())
            .Should().Be(FailureClassification.Cancelled);
    }

    [Fact]
    public void An_unrecognised_exception_is_permanent()
    {
        new DefaultFailureClassifier()
            .Classify(new FormatException())
            .Should().Be(FailureClassification.Permanent,
                "retry-unless-proven-otherwise retries validation errors, authorisation failures and " +
                "business-rule violations, none of which will ever succeed");
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.RequestTimeout, FailureClassification.Transient)]
    [InlineData(System.Net.HttpStatusCode.TooManyRequests, FailureClassification.Transient)]
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable, FailureClassification.Transient)]
    [InlineData(System.Net.HttpStatusCode.BadRequest, FailureClassification.Permanent)]
    [InlineData(System.Net.HttpStatusCode.NotFound, FailureClassification.Permanent)]
    public void Http_status_codes_decide_transience(
        System.Net.HttpStatusCode status, FailureClassification expected)
    {
        new DefaultFailureClassifier()
            .Classify(new HttpRequestException("failed", null, status))
            .Should().Be(expected);
    }
}

/// <summary>The bulkhead's refusal behaviour.</summary>
public sealed class BulkheadTests
{
    [Fact]
    public async Task Calls_up_to_capacity_are_admitted()
    {
        using var bulkhead = new Bulkhead("test", capacity: 2);

        var first = await bulkhead.EnterAsync();
        var second = await bulkhead.EnterAsync();

        bulkhead.Snapshot().InUse.Should().Be(2);

        first.Dispose();
        second.Dispose();

        bulkhead.Snapshot().InUse.Should().Be(0);
    }

    [Fact]
    public async Task A_call_beyond_capacity_with_no_queue_is_refused_immediately()
    {
        using var bulkhead = new Bulkhead("test", capacity: 1, queueLimit: 0);

        using var held = await bulkhead.EnterAsync();

        var refused = async () => await bulkhead.EnterAsync();

        await refused.Should().ThrowAsync<BulkheadFullException>(
            "an unbounded queue converts 'too many concurrent calls' into 'unbounded memory and " +
            "unbounded latency', which is the same outage arriving more slowly");

        bulkhead.Snapshot().TotalRejections.Should().Be(1);
    }

    [Fact]
    public async Task A_released_slot_admits_a_queued_caller()
    {
        using var bulkhead = new Bulkhead("test", capacity: 1, queueLimit: 1);

        var held = await bulkhead.EnterAsync();
        var queued = bulkhead.EnterAsync();

        queued.IsCompleted.Should().BeFalse();

        held.Dispose();

        (await queued).Dispose();
    }
}
