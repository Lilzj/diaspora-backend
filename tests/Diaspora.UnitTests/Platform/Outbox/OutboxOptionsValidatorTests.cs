using Diaspora.Platform.Messaging.Outbox.Options;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The configurations that pass range validation and then misbehave at three in the morning.
/// <para>
/// Every case here is a plausible setting somebody would write on purpose. That is what makes
/// them worth failing the deployment over: a value out of range is a typo somebody notices, and
/// a publish timeout longer than its own lease is a considered decision whose consequence — two
/// workers publishing the same message and the slower one's failure overwriting the faster
/// one's success — is invisible until it happens.
/// </para>
/// </summary>
public sealed class OutboxOptionsValidatorTests
{
    private static readonly OutboxOptionsValidator Validator = new();

    [Fact]
    public void The_shipped_defaults_are_valid()
    {
        Validator.Validate(null, new OutboxOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_publish_timeout_that_outlives_its_lease_is_refused()
    {
        var options = new OutboxOptions();
        options.Dispatch.LeaseSeconds = 30;
        options.Dispatch.PublishTimeoutSeconds = 30;

        var result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PublishTimeoutSeconds");
    }

    /// <summary>
    /// Partitions already run in parallel; raising concurrency only adds it inside a partition,
    /// which is exactly the ordering the same configuration asked for.
    /// </summary>
    [Fact]
    public void Concurrency_inside_a_partition_is_refused_while_ordering_is_on()
    {
        var options = new OutboxOptions();
        options.Dispatch.Ordering = OutboxOrderingMode.Partitioned;
        options.Dispatch.MaxConcurrency = 8;

        var result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxConcurrency");
    }

    [Fact]
    public void Concurrency_is_permitted_when_no_ordering_is_promised()
    {
        var options = new OutboxOptions();
        options.Dispatch.Ordering = OutboxOrderingMode.None;
        options.Dispatch.MaxConcurrency = 8;

        Validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    /// <summary>
    /// A dead letter purged before anyone replays it is the permanent loss this whole phase
    /// exists to prevent. Reintroducing it through a retention setting would be a poor joke.
    /// </summary>
    [Fact]
    public void Dead_letters_may_not_be_purged_sooner_than_delivered_messages()
    {
        var options = new OutboxOptions();
        options.Retention.DeliveredRetentionHours = 72;
        options.Retention.DeadLetterRetentionHours = 24;

        var result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DeadLetterRetentionHours");
    }

    [Fact]
    public void A_retry_budget_that_is_entirely_immediate_is_refused()
    {
        var options = new OutboxOptions();
        options.Retry.MaxAttempts = 3;
        options.Retry.ImmediateAttempts = 3;

        var result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ImmediateAttempts");
    }

    /// <summary>
    /// A blank pattern matches every exception, which would dead-letter the first failure of
    /// every message in the platform. Easy to introduce with a trailing comma in a settings file.
    /// </summary>
    [Fact]
    public void A_blank_poison_exception_pattern_is_refused()
    {
        var options = new OutboxOptions();
        options.Retry.PoisonExceptionTypes.Add("  ");

        Validator.Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void A_lease_recovery_interval_longer_than_the_lease_is_refused()
    {
        var options = new OutboxOptions();
        options.Dispatch.LeaseSeconds = 30;
        options.Dispatch.PublishTimeoutSeconds = 10;
        options.Dispatch.LeaseRecoveryIntervalSeconds = 120;

        var result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("LeaseRecoveryIntervalSeconds");
    }

    [Fact]
    public void A_sweep_slower_than_the_retention_window_is_refused()
    {
        var options = new OutboxOptions();
        options.Retention.DeliveredRetentionHours = 1;
        options.Retention.DeadLetterRetentionHours = 2;
        options.Retention.SweepIntervalMinutes = 240;

        var result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SweepIntervalMinutes");
    }

    [Fact]
    public void Health_thresholds_must_be_ordered()
    {
        var options = new OutboxOptions();
        options.Health.DegradedDepth = 100;
        options.Health.UnhealthyDepth = 50;

        Validator.Validate(null, options).Failed.Should().BeTrue();
    }

    /// <summary>
    /// Every failure is reported at once. A validator that stopped at the first one would mean
    /// three restarts to fix three settings, and each restart is a deployment.
    /// </summary>
    [Fact]
    public void Every_problem_is_reported_together()
    {
        var options = new OutboxOptions();
        options.Dispatch.LeaseSeconds = 20;
        options.Dispatch.PublishTimeoutSeconds = 60;
        options.Retention.DeliveredRetentionHours = 100;
        options.Retention.DeadLetterRetentionHours = 50;

        var result = Validator.Validate(null, options);

        result.Failures.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
