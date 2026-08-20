using Diaspora.Persistence.Outbox;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.UnitTests.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// B1.1's central claim, as executable proof: an aggregate and its integration events commit
/// together or not at all.
/// <para>
/// The defect this phase closed had one shape. An aggregate was saved, the transaction
/// committed, and only then were integration events published — so an exception in a publisher,
/// a process killed between the two, or a broker that was briefly unreachable lost a committed
/// business fact permanently, with the aggregate already on disk and nothing anywhere recording
/// that an announcement had been owed. Every test in this file fails against that design.
/// </para>
/// <para>
/// These write a real aggregate through the real <c>DiasporaDbContext</c> — the same audit
/// pipeline, soft-delete conversion and query filters every module saves through — beside a real
/// outbox row, against a real database.
/// </para>
/// </summary>
public sealed class OutboxTransactionalityTests
{
    /// <summary>The happy path, stated as an invariant rather than a scenario.</summary>
    [Fact]
    public async Task An_aggregate_and_its_event_are_written_by_the_same_save()
    {
        using var host = new OutboxTestHost();

        using (var context = host.NewContext())
        {
            context.Add(SoftDeleteTestOrder.Create("ORD-1"));

            new EfOutboxWriter(context, host.Factory())
                .Enqueue(OutboxMessageBuilder.NewEvent("order placed"));

            await context.SaveChangesAsync();
        }

        (await host.MessagesAsync()).Should().ContainSingle();

        using var verification = host.NewContext();

        (await verification.Set<SoftDeleteTestOrder>().CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// The half that used to be impossible. A transaction that rolls back must take the event
    /// with it — otherwise the platform announces something that never happened, which is a
    /// worse failure than losing the announcement.
    /// </summary>
    [Fact]
    public async Task A_rolled_back_transaction_takes_the_event_with_it()
    {
        using var host = new OutboxTestHost();

        using (var context = host.NewContext())
        {
            var act = async () => await context.ExecuteTransactionalAsync<int>(
                async token =>
                {
                    context.Add(SoftDeleteTestOrder.Create("ORD-2"));

                    new EfOutboxWriter(context, host.Factory())
                        .Enqueue(OutboxMessageBuilder.NewEvent("order placed"));

                    await context.SaveChangesAsync(token);

                    // Something later in the same command fails — a downstream call, a
                    // validation, a second aggregate that would not save.
                    throw new InvalidOperationException("the rest of the command failed");
                });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        (await host.MessagesAsync()).Should().BeEmpty();

        using var verification = host.NewContext();

        (await verification.Set<SoftDeleteTestOrder>().IgnoreQueryFilters().CountAsync())
            .Should().Be(0, "the aggregate and the event share one transaction");
    }

    /// <summary>
    /// The mirror image, and the reason the writer refuses an oversized or unserializable event
    /// at enqueue time rather than at dispatch: a defective event takes the aggregate down with
    /// it, loudly, at the call site that produced it.
    /// </summary>
    [Fact]
    public async Task An_event_that_cannot_be_written_fails_the_aggregate_too()
    {
        using var host = new OutboxTestHost(options => options.MaxPayloadBytes = 256);

        using (var context = host.NewContext())
        {
            var act = async () => await context.ExecuteTransactionalAsync<int>(
                async token =>
                {
                    context.Add(SoftDeleteTestOrder.Create("ORD-3"));

                    new EfOutboxWriter(context, host.Factory()).Enqueue(
                        OutboxMessageBuilder.NewEvent(new string('x', 4096)));

                    return await context.SaveChangesAsync(token);
                });

            (await act.Should().ThrowAsync<InvalidOperationException>())
                .Which.Message.Should().Contain("ceiling");
        }

        using var verification = host.NewContext();

        (await verification.Set<SoftDeleteTestOrder>().IgnoreQueryFilters().CountAsync())
            .Should().Be(0);
    }

    /// <summary>
    /// A message is committed before anything tries to publish it. Restated as the property that
    /// matters operationally: the row is durable at commit, so a process killed one instruction
    /// after the commit loses nothing.
    /// </summary>
    [Fact]
    public async Task A_committed_event_survives_the_process_that_wrote_it()
    {
        using var host = new OutboxTestHost();

        using (var context = host.NewContext())
        {
            new EfOutboxWriter(context, host.Factory())
                .Enqueue(OutboxMessageBuilder.NewEvent("survives"));

            await context.SaveChangesAsync();
        }

        // A different context, an empty change tracker — the closest a test gets to "the
        // process that wrote this is gone".
        var recovered = await host.MessagesAsync();

        recovered.Should().ContainSingle();
        recovered[0].State.Should().Be(OutboxState.Pending);
        recovered[0].Payload.Should().Contain("survives");
    }

    /// <summary>
    /// The enqueue captures the producing request's correlation identifier, because the
    /// dispatcher that eventually publishes it runs on a background thread with no request
    /// behind it and anything it read from ambient context would belong to nobody.
    /// </summary>
    [Fact]
    public async Task The_producing_requests_correlation_travels_with_the_message()
    {
        using var host = new OutboxTestHost();

        host.Correlation.CorrelationId = "request-abc";

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("correlated"));

        (await host.MessagesAsync())[0].CorrelationId.Should().Be("request-abc");
    }

    /// <summary>
    /// The idempotency key stored is the event's own identifier, unchanged. Consumers are told
    /// to deduplicate on it; if the outbox minted its own, that guidance would be wrong.
    /// </summary>
    [Fact]
    public async Task The_stored_message_identifier_is_the_events_own()
    {
        using var host = new OutboxTestHost();

        var integrationEvent = OutboxMessageBuilder.NewEvent("identified");

        await host.EnqueueAsync(integrationEvent);

        (await host.MessagesAsync())[0].MessageId.Should().Be(integrationEvent.EventId);
    }

    /// <summary>
    /// The unique index on the identifier is a guard, not decoration: two rows for one event
    /// would be a duplicate every consumer had to reconcile, and the database is the only place
    /// that can refuse it under concurrency.
    /// </summary>
    [Fact]
    public async Task The_same_event_cannot_be_enqueued_twice()
    {
        using var host = new OutboxTestHost();

        var integrationEvent = OutboxMessageBuilder.NewEvent("duplicated");

        await host.EnqueueAsync(integrationEvent);

        var act = async () => await host.EnqueueAsync(integrationEvent);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
