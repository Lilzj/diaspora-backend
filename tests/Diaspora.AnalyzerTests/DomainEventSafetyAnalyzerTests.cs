using FluentAssertions;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// The four domain-event rules, each with the shape it forbids and the shape it must leave alone.
/// <para>
/// <b>Every rule here is tested from both sides, and the second half is the one that matters.</b>
/// A suite of "this is reported" assertions stays green if the analyzer starts reporting
/// everything, which is how a guardrail becomes noise nobody reads. The companion test in each
/// pair compiles the pattern the platform actually uses and asserts silence.
/// </para>
/// </summary>
public sealed class DomainEventSafetyAnalyzerTests
{
    // ── DSP1001: handler-to-handler ──────────────────────────────────────

    [Fact]
    public void A_handler_that_injects_another_handler_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Shared : DomainEventHandler<OtherThingHappened>
            {
                protected override Task HandleAsync(OtherThingHappened e, CancellationToken ct) =>
                    Task.CompletedTask;
            }

            public sealed class Reactor : DomainEventHandler<ThingHappened>
            {
                private readonly Shared _shared;

                public Reactor(Shared shared) => _shared = shared;

                protected override Task HandleAsync(ThingHappened e, CancellationToken ct) =>
                    Task.CompletedTask;
            }
            """));

        ids.Should().Contain("DSP1001");
    }

    /// <summary>
    /// The mediator is the same defect with an indirection in front of it: a handler holding
    /// <c>ISender</c> can reach every handler in the process, and the settlement order becomes
    /// whatever the call graph happens to be that day.
    /// </summary>
    [Fact]
    public void A_handler_that_injects_the_mediator_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Reactor : DomainEventHandler<ThingHappened>
            {
                private readonly ISender _sender;

                public Reactor(ISender sender) => _sender = sender;

                protected override Task HandleAsync(ThingHappened e, CancellationToken ct) =>
                    Task.CompletedTask;
            }
            """));

        ids.Should().Contain("DSP1001");
    }

    /// <summary>
    /// The pattern B7 arrived at and the rule exists to preserve: the shared behaviour lives in a
    /// service, and both handlers take the service.
    /// </summary>
    [Fact]
    public void A_handler_that_injects_a_shared_service_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Reconciler
            {
                public Task ReconcileAsync(CancellationToken ct) => Task.CompletedTask;
            }

            public sealed class Reactor : DomainEventHandler<ThingHappened>
            {
                private readonly Reconciler _reconciler;

                public Reactor(Reconciler reconciler) => _reconciler = reconciler;

                protected override Task HandleAsync(ThingHappened e, CancellationToken ct) =>
                    _reconciler.ReconcileAsync(ct);
            }
            """));

        ids.Should().BeEmpty();
    }

    // ── DSP1002: two settlers, one domain event ──────────────────────────

    [Fact]
    public void Two_handlers_settling_one_domain_event_are_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class First : DomainEventHandler<ThingHappened>
            {
                private readonly IUnitOfWork _unitOfWork;

                public First(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                protected override Task HandleAsync(ThingHappened e, CancellationToken ct) =>
                    _unitOfWork.SaveChangesAsync(ct);
            }

            public sealed class Second : DomainEventHandler<ThingHappened>
            {
                private readonly IUnitOfWork _unitOfWork;

                public Second(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                protected override Task HandleAsync(ThingHappened e, CancellationToken ct) =>
                    _unitOfWork.SaveChangesAsync(ct);
            }
            """));

        ids.Should().ContainSingle().Which.Should().Be("DSP1002");
    }

    /// <summary>
    /// <b>The low-noise half of the rule.</b> Two handlers for one event is a normal, useful
    /// shape — one settles, the other emits telemetry or projects a read model. Only the second
    /// settlement is ambiguity, so only the second settlement is reported.
    /// </summary>
    [Fact]
    public void A_second_handler_that_does_not_settle_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Settler : DomainEventHandler<ThingHappened>
            {
                private readonly IUnitOfWork _unitOfWork;

                public Settler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                protected override Task HandleAsync(ThingHappened e, CancellationToken ct) =>
                    _unitOfWork.SaveChangesAsync(ct);
            }

            public sealed class Observer : DomainEventHandler<ThingHappened>
            {
                protected override Task HandleAsync(ThingHappened e, CancellationToken ct)
                {
                    Console.WriteLine(e.EventId);
                    return Task.CompletedTask;
                }
            }
            """));

        ids.Should().BeEmpty();
    }

    // ── DSP1003: two settlers, one integration event, one module ─────────

    [Fact]
    public void Two_consumers_settling_one_integration_event_in_a_module_are_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Projector : IntegrationEventHandler<ThingPublished>
            {
                private readonly IThingRepository _things;

                public Projector(IThingRepository things) => _things = things;

                protected override Task HandleAsync(ThingPublished e, CancellationToken ct)
                {
                    _things.Add(new Thing());
                    return Task.CompletedTask;
                }
            }

            public sealed class Counter : IntegrationEventHandler<ThingPublished>
            {
                private readonly IThingRepository _things;

                public Counter(IThingRepository things) => _things = things;

                protected override Task HandleAsync(ThingPublished e, CancellationToken ct)
                {
                    _things.Add(new Thing());
                    return Task.CompletedTask;
                }
            }
            """));

        ids.Should().ContainSingle().Which.Should().Be("DSP1003");
    }

    /// <summary>
    /// Fan-out across modules is the entire point of a published contract, and the rule cannot see
    /// it: one module is one compilation, so the same event settled in Guides and in Notifications
    /// is two separate runs of the analyzer that never meet.
    /// </summary>
    [Fact]
    public void One_settling_consumer_per_module_is_left_alone()
    {
        var probe = Probes.File(
            """
            public sealed class Projector : IntegrationEventHandler<ThingPublished>
            {
                private readonly IThingRepository _things;

                public Projector(IThingRepository things) => _things = things;

                protected override Task HandleAsync(ThingPublished e, CancellationToken ct)
                {
                    _things.Add(new Thing());
                    return Task.CompletedTask;
                }
            }
            """);

        AnalyzerHarness.Ids(probe, "Diaspora.Modules.Guides").Should().BeEmpty();
        AnalyzerHarness.Ids(probe, "Diaspora.Modules.Notifications").Should().BeEmpty();
    }

    // ── DSP1004: one type, two pipelines ─────────────────────────────────

    /// <summary>
    /// <b>The hole the B1.2 compile-time separation leaves open.</b> The base classes each take one
    /// type argument, so a handler serving several events implements
    /// <c>INotificationHandler&lt;…Notification&lt;T&gt;&gt;</c> directly — which Marketplace does,
    /// legitimately, seven times. Nothing then stops the two wrappers appearing on one type, and a
    /// type that does that is written against one pipeline's guarantees and gets the other's for
    /// half its methods.
    /// </summary>
    [Fact]
    public void A_handler_serving_both_pipelines_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Mixed :
                INotificationHandler<DomainEventNotification<ThingHappened>>,
                INotificationHandler<IntegrationEventNotification<ThingPublished>>
            {
                public Task Handle(DomainEventNotification<ThingHappened> n, CancellationToken ct) =>
                    Task.CompletedTask;

                public Task Handle(IntegrationEventNotification<ThingPublished> n, CancellationToken ct) =>
                    Task.CompletedTask;
            }
            """));

        ids.Should().Contain("DSP1004");
    }

    /// <summary>
    /// Marketplace's actual shape: several domain events, one handler, one pipeline. Reporting this
    /// would have made the rule unusable in the module that needs it most.
    /// </summary>
    [Fact]
    public void A_handler_serving_several_events_on_one_pipeline_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Lifecycle :
                INotificationHandler<DomainEventNotification<ThingHappened>>,
                INotificationHandler<DomainEventNotification<OtherThingHappened>>
            {
                public Task Handle(DomainEventNotification<ThingHappened> n, CancellationToken ct) =>
                    Task.CompletedTask;

                public Task Handle(DomainEventNotification<OtherThingHappened> n, CancellationToken ct) =>
                    Task.CompletedTask;
            }
            """));

        ids.Should().BeEmpty();
    }
}
