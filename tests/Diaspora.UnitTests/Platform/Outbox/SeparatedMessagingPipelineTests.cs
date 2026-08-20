using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Infrastructure.Identity.Events;
using Diaspora.Infrastructure.Messaging;
using Diaspora.Infrastructure.Services;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The two pipelines, end to end, after B1.2 separated them.
/// <para>
/// <b>This file replaces <c>OutboxRoutingDomainEventDispatcherTests</c>, and the replacement is
/// the point of the phase.</b> That suite pinned a compensating control: a wrapper around
/// <c>IDomainEventDispatcher</c> that inspected each event, sent the integration ones to the
/// outbox and passed the rest through. B1.1 needed it because an integration event was also a
/// domain event, so Trust and Community could — and did — reach the dispatcher directly with 29
/// of the platform's 42 contracts. The wrapper worked. It was also a runtime check standing in
/// for a type-system guarantee, and it had to be correct forever on a path every command in the
/// platform runs.
/// </para>
/// <para>
/// The wrapper is gone. What replaced it is not a better check but the absence of anything to
/// check: an integration event cannot enter the domain pipeline, so nothing has to notice when
/// one does. The tests below therefore assert the two pipelines behave correctly, and the
/// assertion that used to matter most — "the router sent this one to the right place" — has no
/// counterpart here because it has no failure mode left.
/// </para>
/// </summary>
public sealed class SeparatedMessagingPipelineTests
{
    private readonly IPublisher _mediator = Substitute.For<IPublisher>();
    private readonly IOutboxWriter _outbox = Substitute.For<IOutboxWriter>();

    private MediatRIntegrationEventDispatcher IntegrationDispatcher() =>
        new(_mediator, NullLogger<MediatRIntegrationEventDispatcher>.Instance);

    private MediatRDomainEventDispatcher DomainDispatcher() =>
        new(_mediator, NullLogger<MediatRDomainEventDispatcher>.Instance);

    private OutboxIntegrationEventPublisher Publisher(bool outboxEnabled = true) =>
        new(
            _outbox,
            IntegrationDispatcher(),
            Options.Create(new OutboxOptions { Enabled = outboxEnabled }),
            NullLogger<OutboxIntegrationEventPublisher>.Instance);

    // ── The producer's route ─────────────────────────────────────────────

    /// <summary>
    /// The route Trust and Community now take, and the one B1.1 could not make them take.
    /// Publishing writes a row and runs nothing: no consumer executes inside the producer's
    /// transaction.
    /// </summary>
    [Fact]
    public async Task Publishing_enqueues_and_runs_no_consumer()
    {
        var published = OutboxMessageBuilder.NewEvent("published");

        await Publisher().PublishAsync(published);

        _outbox.Received(1).Enqueue(published);

        await _mediator.DidNotReceive().Publish(
            Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The documented rollback path restores pre-B1.1 behaviour in full, data loss included.
    /// B1.2 changed how it gets there — through the integration dispatcher rather than the
    /// domain one — and not what it does.
    /// </summary>
    [Fact]
    public async Task With_the_outbox_disabled_publishing_delivers_in_process()
    {
        await Publisher(outboxEnabled: false).PublishAsync(OutboxMessageBuilder.NewEvent("legacy"));

        _outbox.DidNotReceive().Enqueue(Arg.Any<IIntegrationEvent>());

        await _mediator.Received(1).Publish(
            Arg.Any<IntegrationEventNotification<WidgetChangedIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The unregistered rollback publisher, kept in the tree as the escape hatch. It was retyped
    /// in B1.2 because the domain dispatcher stopped accepting its argument — one of the call
    /// sites the compiler found rather than a reviewer.
    /// </summary>
    [Fact]
    public async Task The_rollback_publisher_delivers_in_process()
    {
        await new MediatRIntegrationEventPublisher(IntegrationDispatcher())
            .PublishAsync(OutboxMessageBuilder.NewEvent("rollback"));

        await _mediator.Received(1).Publish(
            Arg.Any<IntegrationEventNotification<WidgetChangedIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }

    // ── The two pipelines wrap into different notifications ──────────────

    /// <summary>
    /// <b>The separation, observed at the seam where it could most easily be undone.</b> A single
    /// wrapper for both kinds would be less code and would compile; it would also mean one
    /// notification reaching both pipelines' handlers, with the winner decided by registration
    /// order.
    /// <para>
    /// The natural companion assertion — that the event is <em>not</em> also wrapped in
    /// <c>DomainEventNotification&lt;WidgetChangedIntegrationEvent&gt;</c> — cannot be written
    /// here: naming that closed generic is CS0311, because the wrapper's constraint no longer
    /// admits an integration event. That is the guarantee holding, and it is asserted where such
    /// a thing can be asserted, in
    /// <c>MessagingCompilerEnforcementTests.Wrapping_an_integration_event_in_the_domain_notification_does_not_compile</c>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_integration_event_is_wrapped_in_the_integration_notification()
    {
        await IntegrationDispatcher().DispatchAsync([OutboxMessageBuilder.NewEvent("delivered")]);

        await _mediator.Received(1).Publish(
            Arg.Any<IntegrationEventNotification<WidgetChangedIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_domain_event_is_wrapped_in_the_domain_notification()
    {
        await DomainDispatcher().DispatchAsync([new PipelineProbeDomainEvent()]);

        await _mediator.Received(1).Publish(
            Arg.Any<DomainEventNotification<PipelineProbeDomainEvent>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The domain pipeline never touches the outbox. Before B1.2 this was a property of the
    /// routing wrapper's <c>if</c>; it is now a property of the signature, and the assertion
    /// survives only as a regression guard against somebody reintroducing the coupling.
    /// </summary>
    [Fact]
    public async Task The_domain_pipeline_never_reaches_the_outbox()
    {
        await DomainDispatcher().DispatchAsync(
            [new PipelineProbeDomainEvent(), new PipelineProbeDomainEvent()]);

        _outbox.DidNotReceive().Enqueue(Arg.Any<IIntegrationEvent>());

        await _mediator.Received(2).Publish(
            Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    // ── Ordering and failure ─────────────────────────────────────────────

    /// <summary>
    /// Several events published in one handler are enqueued in call order, which is what makes
    /// the partition-key ordering guarantee mean anything: the outbox orders within a partition
    /// by sequence, and the sequence follows the order rows were added.
    /// </summary>
    [Fact]
    public async Task Publishing_preserves_call_order()
    {
        var publisher = Publisher();

        var first = OutboxMessageBuilder.NewEvent("first");
        var second = OutboxMessageBuilder.NewEvent("second");

        await publisher.PublishAsync(first);
        await publisher.PublishAsync(second);

        Received.InOrder(() =>
        {
            _outbox.Enqueue(first);
            _outbox.Enqueue(second);
        });
    }

    /// <summary>
    /// <b>A publish failure takes the transaction with it, and that is the intended behaviour.</b>
    /// Enqueuing fails only when the event cannot be serialized or is oversized — defects in the
    /// event itself. Committing the aggregate anyway would produce a decision nobody was ever
    /// told about, which is the failure mode the outbox exists to prevent.
    /// </summary>
    [Fact]
    public async Task A_failed_enqueue_propagates_to_the_caller()
    {
        _outbox.Enqueue(Arg.Any<IIntegrationEvent>())
            .Returns(_ => throw new InvalidOperationException("payload too large"));

        var publish = async () =>
            await Publisher().PublishAsync(OutboxMessageBuilder.NewEvent("oversized"));

        await publish.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// A consumer that throws propagates to the transport, which is what tells the dispatcher to
    /// retry and eventually dead-letter. A dispatcher that swallowed here would assert a
    /// delivery that never happened, and the message would be marked delivered on the strength
    /// of it.
    /// </summary>
    [Fact]
    public async Task A_failing_consumer_propagates_out_of_the_integration_dispatcher()
    {
        _mediator.Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("consumer failed")));

        var deliver = async () =>
            await IntegrationDispatcher().DispatchAsync([OutboxMessageBuilder.NewEvent("failing")]);

        await deliver.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>A domain event with no integration payload, for the domain-pipeline assertions.</summary>
    private sealed record PipelineProbeDomainEvent : DomainEvent;
}
