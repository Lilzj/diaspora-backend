using FluentAssertions;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// The messaging rules, including the two that depend on which module the code is in.
/// </summary>
public sealed class MessagingBoundaryAnalyzerTests
{
    /// <summary>
    /// <b>The regression B1.2 removed, attempted again.</b> Before that phase every published
    /// contract was a domain event, and the domain dispatcher accepted all of them. The base
    /// records no longer inherit each other, but nothing stops a type implementing both interfaces
    /// by hand — and one that did would restore every route the phase closed.
    /// </summary>
    [Fact]
    public void A_type_that_is_both_kinds_of_event_is_an_error()
    {
        var diagnostics = AnalyzerHarness.Analyze(Probes.File(
            """
            public sealed record Hybrid : IDomainEvent, IIntegrationEvent
            {
                public Guid EventId { get; init; }
                public DateTimeOffset OccurredAt { get; init; }
            }
            """));

        var reported = diagnostics.Should().ContainSingle().Subject;

        reported.Id.Should().Be("DSP2001");
        reported.Severity.Should().Be(Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
            "a type that rejoins the two hierarchies is not a style question");
    }

    [Fact]
    public void The_two_event_kinds_kept_apart_are_left_alone()
    {
        AnalyzerHarness.Ids(Probes.File(
            """
            public sealed record Fine : DomainEvent;
            """)).Should().BeEmpty();
    }

    /// <summary>
    /// A module naming the outbox is a module publishing outside its own transaction. The rule
    /// matches the name rather than the symbol on purpose: no module can reference the outbox
    /// today, so a semantic check would resolve nothing and see nothing — the point is to catch the
    /// reference at the moment somebody adds it.
    /// </summary>
    [Fact]
    public void A_module_reaching_the_transport_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public interface IOutboxWriter
            {
                Task WriteAsync(CancellationToken ct);
            }

            public sealed class Publisher
            {
                private readonly IOutboxWriter _outbox;

                public Publisher(IOutboxWriter outbox) => _outbox = outbox;
            }
            """));

        ids.Should().Contain("DSP2002");
    }

    [Fact]
    public void A_module_publishing_through_the_port_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Announcer
            {
                private readonly IIntegrationEventPublisher _publisher;

                public Announcer(IIntegrationEventPublisher publisher) => _publisher = publisher;

                public Task AnnounceAsync(CancellationToken ct) =>
                    _publisher.PublishAsync(new ThingPublished { Name = "x" }, ct);
            }
            """));

        ids.Should().BeEmpty();
    }

    /// <summary>
    /// A class holding a notification dispatcher, declared in the probe so the snippet compiles.
    /// <para>
    /// <b>The type is declared rather than referenced, and that is the rule's design rather than a
    /// convenience.</b> <c>DSP2003</c> matches the simple identifier, not a resolved symbol,
    /// precisely so it can fire on a name that resolves to nothing — which is what
    /// <c>INotificationDispatcher</c> does on this platform, and what
    /// <c>INotificationPublisher</c> did from a vertical before B1.4.1 obsoleted it. Catching the
    /// name at the moment somebody types it is the whole value; waiting for a project reference
    /// that would make it resolve is waiting until after the decision was made.
    /// </para>
    /// <para>
    /// It also has to be a name that is not obsoleted, because since B1.4.1 naming
    /// <c>INotificationPublisher</c> is an <c>[Obsolete(error: true)]</c> compile error under
    /// <c>DSP2004</c> — one that <c>#pragma warning disable</c> cannot lift, because a pragma
    /// suppresses warnings and this is an error. The harness rightly refuses a probe that does not
    /// compile. <see cref="Naming_the_obsoleted_notification_port_does_not_compile"/> asserts that
    /// directly.
    /// </para>
    /// </summary>
    private const string NamesTheNotificationWritePath =
        """
        public interface INotificationDispatcher
        {
        }

        public sealed class Notifier
        {
            private readonly INotificationDispatcher _dispatcher;

            public Notifier(INotificationDispatcher dispatcher) => _dispatcher = dispatcher;
        }
        """;

    /// <summary>
    /// <b>The B13 boundary, from the vertical's side.</b> A module that reaches for the
    /// notification write path has decided who to contact and how, without seeing the member's
    /// channel preferences, quiet hours or digest window — none of which it can reach.
    /// </summary>
    [Fact]
    public void A_business_module_naming_the_notification_write_path_is_reported()
    {
        var ids = AnalyzerHarness.Ids(
            Probes.File(NamesTheNotificationWritePath), AnalyzerHarness.BusinessModule);

        ids.Should().Contain("DSP2003");
    }

    /// <summary>
    /// The same code inside the Communication Platform is the Communication Platform doing its job.
    /// The rule is a statement about which assembly the code is in, and this is the half that
    /// proves it is not simply matching a name everywhere.
    /// </summary>
    [Fact]
    public void The_communication_platform_may_name_its_own_write_path()
    {
        var ids = AnalyzerHarness.Ids(
            Probes.File(NamesTheNotificationWritePath), AnalyzerHarness.Communication);

        ids.Should().BeEmpty();
    }

    /// <summary>
    /// <b>The kernel's dormant port, from every side at once.</b> B1.4 recorded
    /// <c>INotificationPublisher</c> as finding F5 — declared in B1, never implemented, never
    /// called — and B1.4.1 obsoleted it as an error rather than implementing or deleting it.
    /// <para>
    /// <b>Including from the Communication Platform.</b> <c>DSP2003</c> exempts B13, correctly,
    /// because B13 owns the thing the rule protects — which leaves B13 as the one module that could
    /// implement the port, and an implementation would be a kernel-level notification path open to
    /// every module at once. The obsoletion has no exemption, and this asserts it.
    /// </para>
    /// <para>
    /// Asserted through the harness's own refusal, which is the honest signal: a probe naming this
    /// type produces no usable semantic model, from any assembly, and no <c>#pragma</c> lifts it —
    /// a pragma suppresses warnings, and this is an error.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(AnalyzerHarness.BusinessModule)]
    [InlineData(AnalyzerHarness.Communication)]
    public void Naming_the_obsoleted_notification_port_does_not_compile(string assembly)
    {
        var act = () => AnalyzerHarness.Ids(
            Probes.File(
                """
                public sealed class Notifier
                {
                    private readonly Diaspora.Platform.Services.Abstractions.INotificationPublisher _publisher;

                    public Notifier(Diaspora.Platform.Services.Abstractions.INotificationPublisher publisher) =>
                        _publisher = publisher;
                }
                """),
            assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(
                "*DSP2004*",
                "the port is obsoleted as an error, so the shortcut is answered where it is taken");
    }
}
