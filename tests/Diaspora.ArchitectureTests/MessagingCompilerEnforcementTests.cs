using System.Collections.Immutable;
using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The B1.2 guarantee, proved rather than described.
/// <para>
/// <b>Why this suite exists in this form.</b> The phase's claim is that the messaging boundary is
/// enforced by the compiler — which means the evidence for it is code that <em>does not compile</em>,
/// and code that does not compile cannot be checked into a test project. Every other way of
/// pinning this degrades into something weaker: a commented-out snippet nobody runs, a reflection
/// assertion about type hierarchies that says nothing about what the compiler would accept, or an
/// architecture rule that catches the violation after it was written rather than preventing it.
/// </para>
/// <para>
/// So each test compiles a real snippet in memory, against the real platform assemblies, and
/// asserts the exact diagnostic. If a future change quietly restores substitutability — an
/// interface widened, a base class reinstated, an implicit conversion added — these tests fail by
/// reporting that the illegal code compiled, which is the failure that matters and the one no
/// green suite would otherwise notice.
/// </para>
/// <para>
/// The counterpart matters as much: <see cref="The_legal_route_compiles"/> compiles the correct
/// code. Without it, a suite of "this must not compile" assertions would stay green if the
/// messaging types were deleted outright.
/// </para>
/// <para>
/// See <c>docs/platform/b1.2-compiler-enforcement-report.md</c>.
/// </para>
/// </summary>
public sealed class MessagingCompilerEnforcementTests
{
    // ── The four illegal routes ──────────────────────────────────────────

    /// <summary>
    /// <b>The exact line that shipped in Trust and Community before B1.2.</b> It compiled, it
    /// worked, and it bypassed the port whose documentation said it was the only way out — which
    /// is why B1.1 had to intercept the domain dispatcher instead of the publisher. The
    /// collection expression cannot be converted, because an integration event is no longer an
    /// <c>IDomainEvent</c>.
    /// </summary>
    [Fact]
    public void Dispatching_an_integration_event_through_the_domain_pipeline_does_not_compile()
    {
        var diagnostics = Compile(
            """
            var dispatcher = (IDomainEventDispatcher)null!;
            var published = new TrustLevelChangedIntegrationEvent
            {
                UserId = Guid.NewGuid(),
                PreviousLevel = "Basic",
                CurrentLevel = "Verified",
                PlatformTrustLevel = "Verified",
                Reason = "test",
                ChangedAt = DateTimeOffset.UtcNow
            };

            await dispatcher.DispatchAsync([published]);
            """);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(
                "CS0029",
                "the collection expression cannot convert an integration event to IDomainEvent");
    }

    /// <summary>
    /// The consumer half. Location and Providers subscribed to Trust's contracts by deriving
    /// <c>DomainEventHandler&lt;T&gt;</c>, which read as though one module were handling another's
    /// private facts. The constraint now refuses it.
    /// </summary>
    [Fact]
    public void Handling_an_integration_event_as_a_domain_event_does_not_compile()
    {
        var diagnostics = Compile(
            declarations:
            """
            sealed class Illegal : DomainEventHandler<TrustLevelChangedIntegrationEvent>
            {
                protected override Task HandleAsync(
                    TrustLevelChangedIntegrationEvent e, CancellationToken ct) => Task.CompletedTask;
            }
            """);

        diagnostics.Should().Contain(
            diagnostic => diagnostic.Id == "CS0311",
            "TDomainEvent : IDomainEvent no longer admits an integration event");
    }

    /// <summary>
    /// The same boundary from the other side. A domain event is a module's private business and
    /// has no published payload, so publishing one would write a contract nobody agreed to — and
    /// would serialize the module's own types into a durable row.
    /// </summary>
    [Fact]
    public void Publishing_a_domain_event_as_an_integration_event_does_not_compile()
    {
        var diagnostics = Compile(
            """
            var publisher = (IIntegrationEventPublisher)null!;

            await publisher.PublishAsync(new ProbeDomainEvent());
            """,
            declarations: "sealed record ProbeDomainEvent : DomainEvent;");

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CS1503", "the publisher accepts IIntegrationEvent alone");
    }

    /// <summary>
    /// And the wrapper types, which are what a hand-rolled handler registration would reach for.
    /// A single wrapper constrained to <c>IMessage</c> would have compiled here and quietly
    /// restored the whole defect one layer down.
    /// </summary>
    [Fact]
    public void Wrapping_an_integration_event_in_the_domain_notification_does_not_compile()
    {
        var diagnostics = Compile(
            """
            var published = new TrustLevelChangedIntegrationEvent
            {
                UserId = Guid.NewGuid(),
                PreviousLevel = "Basic",
                CurrentLevel = "Verified",
                PlatformTrustLevel = "Verified",
                Reason = "test",
                ChangedAt = DateTimeOffset.UtcNow
            };

            var wrapped = new DomainEventNotification<TrustLevelChangedIntegrationEvent>(published);
            """);

        diagnostics.Should().Contain(
            diagnostic => diagnostic.Id == "CS0311",
            "the two notification wrappers are separate so neither can carry the other's payload");
    }

    // ── The legal route ──────────────────────────────────────────────────

    /// <summary>
    /// <b>The control.</b> Every assertion above is satisfied by deleting the messaging types
    /// altogether; this one is not. It compiles the migration the whole platform performed — a
    /// producer publishing through the port, a consumer deriving the integration handler, and a
    /// domain event still travelling the domain pipeline — so the suite proves that the boundary
    /// exists and is usable, not merely that something failed to build.
    /// </summary>
    [Fact]
    public void The_legal_route_compiles()
    {
        var diagnostics = Compile(
            """
            var publisher = (IIntegrationEventPublisher)null!;
            var dispatcher = (IDomainEventDispatcher)null!;

            await publisher.PublishAsync(new TrustLevelChangedIntegrationEvent
            {
                UserId = Guid.NewGuid(),
                PreviousLevel = "Basic",
                CurrentLevel = "Verified",
                PlatformTrustLevel = "Verified",
                Reason = "test",
                ChangedAt = DateTimeOffset.UtcNow
            });

            await dispatcher.DispatchAsync([new ProbeDomainEvent()]);
            """,
            declarations:
            """
            sealed record ProbeDomainEvent : DomainEvent;

            sealed class Consumer : IntegrationEventHandler<TrustLevelChangedIntegrationEvent>
            {
                protected override Task HandleAsync(
                    TrustLevelChangedIntegrationEvent e, CancellationToken ct) => Task.CompletedTask;
            }

            sealed class Reactor : DomainEventHandler<ProbeDomainEvent>
            {
                protected override Task HandleAsync(
                    ProbeDomainEvent e, CancellationToken ct) => Task.CompletedTask;
            }
            """);

        diagnostics.Should().BeEmpty(
            "the supported route must compile, or the tests above would pass on an empty platform");
    }

    // ── The compiler harness ─────────────────────────────────────────────

    /// <summary>
    /// Compiles a snippet against the same assemblies a module compiles against and returns the
    /// errors.
    /// <para>
    /// The reference set is taken from the loaded assemblies of the messaging types themselves
    /// rather than from a hard-coded list of paths, so it cannot drift from what the platform
    /// actually ships. <c>allowUnsafe</c> and the language version are left at their defaults on
    /// purpose: the point is what an ordinary module can express, not what is expressible.
    /// </para>
    /// </summary>
    private static ImmutableArray<Diagnostic> Compile(
        string statements = "", string declarations = "")
    {
        var source =
            $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Diaspora.Application.Abstractions;
            using Diaspora.Contracts.Events;
            using Diaspora.Contracts.Trust.V1.Events;
            using Diaspora.Platform.Messaging;
            using Diaspora.Platform.Services.Abstractions;
            using Diaspora.SharedKernel.Primitives;

            namespace MessagingBoundaryProbe;

            {{declarations}}

            static class Probe
            {
                public static async Task RunAsync()
                {
                    {{statements}}

                    await Task.CompletedTask;
                }
            }
            """;

        var compilation = CSharpCompilation.Create(
            assemblyName: "MessagingBoundaryProbe",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: ReferenceSet(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
    }

    private static List<MetadataReference> ReferenceSet()
    {
        // The platform assemblies the snippet names, plus the transitive closure of the runtime
        // they were built against. Walking Assembly.Location of already-loaded assemblies keeps
        // the reference set honest: it is exactly what this test project resolved.
        var roots = new[]
        {
            typeof(object).Assembly,                         // System.Private.CoreLib
            typeof(Console).Assembly,
            typeof(Task).Assembly,
            typeof(IMessage).Assembly,                       // Diaspora.SharedKernel
            typeof(IIntegrationEvent).Assembly,              // Diaspora.Contracts
            typeof(IIntegrationEventPublisher).Assembly,     // Diaspora.Platform
            typeof(DomainEventHandler<>).Assembly,           // Diaspora.Application
            typeof(MediatR.INotification).Assembly
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();

        void Add(Assembly assembly)
        {
            if (string.IsNullOrEmpty(assembly.Location) || !seen.Add(assembly.Location))
            {
                return;
            }

            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        foreach (var assembly in roots)
        {
            Add(assembly);
        }

        // System.Runtime and the rest of the facade assemblies are never named directly but are
        // required for anything to bind. Everything already loaded into this test host covers
        // them, and adding a file twice is what the seen-set is for.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic))
        {
            Add(assembly);
        }

        return references;
    }
}
