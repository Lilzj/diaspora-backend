using System.Reflection;
using Diaspora.Domain.Abstractions;
using Diaspora.Persistence;
using Diaspora.SharedKernel;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// PSR-1 Final Certification's rules.
/// <para>
/// Same standard as every rule set since B1.4: each one forbids a shape the phase found already
/// present in the codebase, not a hypothetical. Two subjects — the background loops that could not
/// be switched off, and the framework scaffolding that survived because it was superseded quietly
/// rather than retired loudly.
/// </para>
/// <para>
/// <b>These are the rules that make the freeze mean something.</b> A certification that fixes six
/// loops and retires seven types, without pinning either, certifies a moment rather than a
/// platform.
/// </para>
/// </summary>
public sealed class PlatformCertificationArchitectureTests
{
    private static readonly Assembly SharedKernelAssembly = typeof(GuardClause).Assembly;

    private static readonly Assembly DomainAssembly = typeof(IUnitOfWork).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(Diaspora.Application.Exceptions.BusinessRuleViolationException).Assembly;

    private static readonly Assembly PersistenceAssembly = typeof(DiasporaDbContext).Assembly;

    /// <summary>
    /// The fifteen types this phase retired, by full name. Referenced as strings deliberately:
    /// every one is <c>[Obsolete(error: true)]</c>, so naming any of them in code — including here —
    /// would not compile. That is the point of the retirement, and a rule that had to be exempted
    /// from it would be evidence the retirement had not worked.
    /// <para>
    /// Two overlapping populations, fifteen types between them. <b>DEBT-072</b> named two —
    /// <c>IDomainEventHandler&lt;T&gt;</c> and <c>IReadRepository&lt;T&gt;</c> — and re-deriving it
    /// against the code grew that to seven. <b>B2.5's DDD-02</b> named fourteen, in eleven entries:
    /// a production-readiness finding that listed the SharedKernel's dead machinery, was never
    /// closed, never reached the Technical Debt Ledger, and therefore survived fifteen phases as
    /// debt nobody was counting. <c>IReadRepository&lt;T&gt;</c> is in both, which is why the union
    /// is fifteen rather than sixteen.
    /// </para>
    /// </summary>
    private static readonly (Assembly Assembly, string TypeName, string Diagnostic)[] Retired =
    [
        (ApplicationAssembly, "Diaspora.Application.Abstractions.IDomainEventHandler`1", "DSP1005"),
        (DomainAssembly, "Diaspora.Domain.Abstractions.IReadRepository`1", "DSP3005"),
        (DomainAssembly, "Diaspora.Domain.Abstractions.IRepository`1", "DSP3005"),
        (PersistenceAssembly, "Diaspora.Persistence.RepositoryBase`1", "DSP3005"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Primitives.IBusinessRule", "DSP4005"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Primitives.BusinessRuleExtensions", "DSP4005"),
        (SharedKernelAssembly,
            "Diaspora.SharedKernel.Primitives.BusinessRuleViolationException", "DSP4005"),

        // B2.5 DDD-02 — the parallel result types. "Six result types where one plus Error
        // suffices … New module authors will copy whichever they find first."
        (SharedKernelAssembly, "Diaspora.SharedKernel.Results.OperationResult", "DSP4005"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Results.GenericOperationResult`1", "DSP4005"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Results.ValidationResult", "DSP4005"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Results.GenericValidationResult`1", "DSP4005"),

        // B2.5 DDD-02 — the unadopted specification pattern, and its three combinators.
        (SharedKernelAssembly, "Diaspora.SharedKernel.Specifications.Specification`1", "DSP3006"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Specifications.AndSpecification`1", "DSP3006"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Specifications.OrSpecification`1", "DSP3006"),
        (SharedKernelAssembly, "Diaspora.SharedKernel.Specifications.NotSpecification`1", "DSP3006")
    ];

    // ── Background loops ─────────────────────────────────────────────────

    /// <summary>
    /// <b>Every background loop can be switched off.</b>
    /// <para>
    /// Six could not when this phase started: <c>CommunitySweeper</c>, <c>MediaSweeper</c>,
    /// <c>MediaProcessingWorker</c>, <c>CommunityLifeLifecycleSweep</c>,
    /// <c>CommunityLifeMembershipSweep</c> and <c>TransactionReceiptRetentionHostedService</c>. The
    /// integration harness disables ten loops with an explicit reason — <em>"a background loop
    /// querying it while a request is mid-command is a concurrent use of a connection SQLite will
    /// not allow"</em> — and that reason applied identically to all six, which ran anyway because
    /// there was no switch to turn.
    /// </para>
    /// <para>
    /// DEBT-049 recorded the consequence three times over: one full run failing four tests with a
    /// 500 on register that four other runs of the same commit passed, and one run aborting inside
    /// <c>RequestDelegateFactory.Create</c> with five hosts up. The ledger named four sweeps and one
    /// of the four does not exist under that name; re-deriving the set from the code found six, one
    /// of them in the platform kernel rather than a module.
    /// </para>
    /// <para>
    /// <b>Subject:</b> any type that constructs a <c>PeriodicTimer</c> — the platform's single idiom
    /// for a recurring loop, used by all thirty-five of them, verified by measurement before this
    /// rule was written. The loops that do not construct one either inherit
    /// <c>PlatformWorker</c> (whose base reads <c>Operations:Workers:{name}:Enabled</c>) or
    /// <c>FinancialSweep</c> (whose base reads <c>IsEnabled</c>), and both bases are covered here
    /// in their own right.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_background_loop_can_be_switched_off()
    {
        var unswitchable = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var type in file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var timers = type.DescendantNodes()
                    .OfType<ObjectCreationExpressionSyntax>()
                    .Where(creation => creation.Type.ToString().Contains(
                        "PeriodicTimer", StringComparison.Ordinal))
                    .ToList();

                if (timers.Count == 0)
                {
                    continue;
                }

                if (!timers.All(IsGuardedBeforeItStarts))
                {
                    unswitchable.Add($"{file.Locate(type)} ({type.Identifier.ValueText})");
                }
            }
        }

        unswitchable.Should().BeEmpty(
            "a background loop with no enable switch cannot be turned off by the deployment that "
            + "needs it off, and cannot be turned off by the integration harness that shares one "
            + "SQLite connection with it — which is how DEBT-049's non-determinism happened. Add a "
            + "boolean to the module's options type and return early from ExecuteAsync");
    }

    /// <summary>
    /// A guard counts only ahead of the timer, in the method that starts it.
    /// <para>
    /// <b>Anywhere in the type was not enough, and "nable" matched too much.</b>
    /// <c>AutomationSchedulerWorker</c> had no switch and passed this rule on the strength of
    /// <c>!automation.IsRunnable(now)</c>, a check about one automation that has nothing to do with
    /// whether the loop runs, in a method other than the one that starts the timer.
    /// </para>
    /// </summary>
    private static bool IsGuardedBeforeItStarts(ObjectCreationExpressionSyntax timer) =>
        timer.FirstAncestorOrSelf<MethodDeclarationSyntax>() is { } method
        && method.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Any(statement => statement.SpanStart < timer.SpanStart
                && IsEnableGuard(statement.Condition.ToString()));

    /// <summary>
    /// The guard shapes the platform actually uses: <c>!_options.EnableExpirySweep</c>,
    /// <c>!options.EnableRetentionSweep</c>, <c>!IsEnabled</c>, <c>!Enabled</c>,
    /// <c>!_options.Enabled || !dispatch.Enabled</c>.
    /// </summary>
    private static bool IsEnableGuard(string condition) =>
        condition.Contains('!', StringComparison.Ordinal)
        && condition.Contains("Enable", StringComparison.Ordinal);

    // ── Retired framework scaffolding ────────────────────────────────────

    /// <summary>
    /// <b>The retired framework abstractions stay retired.</b>
    /// <para>
    /// Each carries <c>[Obsolete(error: true)]</c> with a reserved diagnostic id, which is the
    /// treatment B1.4.1 established for <c>INotificationPublisher</c> and PSR-1.2 recorded as the
    /// intended answer for DEBT-072. Deleting them would be worse: <em>"an empty namespace is an
    /// invitation to declare the interface again, and the next author would have no way to know it
    /// had been considered and refused."</em>
    /// </para>
    /// <para>
    /// Removing the attribute is a visible edit to the kernel in a phase that says so. This rule is
    /// what makes it visible.
    /// </para>
    /// </summary>
    [Fact]
    public void The_retired_framework_abstractions_are_obsolete_as_errors()
    {
        foreach (var (assembly, typeName, diagnostic) in Retired)
        {
            var type = assembly.GetType(typeName);

            type.Should().NotBeNull(
                $"{typeName} is retired rather than deleted, so that the next author who reaches "
                + "for it is answered with the reason rather than an empty namespace");

            var obsolete = type!.GetCustomAttribute<ObsoleteAttribute>();

            obsolete.Should().NotBeNull($"{typeName} must carry [Obsolete]");

            obsolete!.IsError.Should().BeTrue(
                $"{typeName} is superseded by a rule the platform enforces elsewhere, so reaching "
                + "for it must not compile — a warning is a thing people turn off");

            obsolete.DiagnosticId.Should().Be(
                diagnostic,
                $"{typeName}'s id is permanent from the moment it was written down, and a "
                + "suppression or a build log five years from now must still mean what it meant");
        }
    }

    /// <summary>
    /// <b>And nothing implements one.</b>
    /// <para>
    /// The obsoletion is a compiler diagnostic, so this is belt and braces — but the two halves fail
    /// for different reasons and a reader of a failure deserves to know which. A type appearing here
    /// means somebody suppressed the diagnostic rather than that somebody removed it.
    /// </para>
    /// <para>
    /// <b>Why <c>IRepository&lt;T&gt;</c> is in this set at all</b> is the finding worth keeping:
    /// PSR-1.2's audit enumerated interfaces with no implementing type, and this one had
    /// <c>RepositoryBase&lt;T&gt;</c>, so it counted as live. What the count could not see is that
    /// <c>RepositoryBase&lt;T&gt;</c> is abstract with zero subclasses. <b>A stub is worse than an
    /// absence, because every rule written to catch an absence passes over it</b> — the same lesson
    /// <c>IEligibilityService</c> taught PSR-1.2, one layer up.
    /// </para>
    /// </summary>
    [Fact]
    public void Nothing_implements_a_retired_framework_abstraction()
    {
        var assemblies = Diaspora.Platform.Modules.ModuleAssemblyScanner.FindPlatformAssemblies()
            .Where(assembly =>
                assembly.GetName().Name?.EndsWith("Tests", StringComparison.Ordinal) != true)
            .Concat([
                SharedKernelAssembly, DomainAssembly, ApplicationAssembly, PersistenceAssembly
            ])
            .Distinct()
            .ToList();

        foreach (var (assembly, typeName, _) in Retired)
        {
            var retired = assembly.GetType(typeName)!;

            if (retired.IsSealed && !retired.IsInterface && !retired.IsAbstract)
            {
                continue;
            }

            foreach (var candidate in assemblies)
            {
                var implementations = candidate.GetTypes()
                    .Where(type => type is { IsAbstract: false, IsInterface: false })

                    // A retired type may of course be named by another retired type — the three
                    // specification combinators derive from the base that retired them, and
                    // retiring the base without them would leave a live path to a dead abstraction.
                    // The question this rule asks is whether anything *live* still names one.
                    .Where(type => type.GetCustomAttribute<ObsoleteAttribute>() is null)
                    .Where(type => Implements(type, retired))
                    .Select(type => type.FullName)
                    .ToList();

                implementations.Should().BeEmpty(
                    $"{typeName} is retired; {candidate.GetName().Name} names it anyway, which "
                    + "means the compiler diagnostic was suppressed rather than heeded");
            }
        }
    }

    /// <summary>
    /// <b>Exactly one <c>BusinessRuleViolationException</c> can be thrown.</b>
    /// <para>
    /// There were two types with this name — one in <c>Diaspora.Application.Exceptions</c>, one in
    /// <c>Diaspora.SharedKernel.Primitives</c> — and <c>GlobalExceptionHandlingMiddleware</c> mapped
    /// both to the same 422, naming each by full namespace because neither could be written
    /// unqualified. Neither was ever thrown by any handler or aggregate.
    /// </para>
    /// <para>
    /// The SharedKernel one was the throwing half of the retired <c>IBusinessRule</c>
    /// primitive and is now unusable. The Application one stays: it is the supported escape hatch
    /// for a rule failure that genuinely cannot be returned as a value, and retiring an escape hatch
    /// nobody has needed yet would be a different mistake.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_one_business_rule_violation_exception_can_be_thrown()
    {
        var supported = ApplicationAssembly.GetType(
            "Diaspora.Application.Exceptions.BusinessRuleViolationException");

        supported.Should().NotBeNull();

        supported!.GetCustomAttribute<ObsoleteAttribute>().Should().BeNull(
            "the Application exception is the one supported way to raise a rule failure as an "
            + "exception, and the middleware maps it to 422");

        var retired = SharedKernelAssembly.GetType(
            "Diaspora.SharedKernel.Primitives.BusinessRuleViolationException");

        retired!.GetCustomAttribute<ObsoleteAttribute>()!.IsError.Should().BeTrue(
            "two live types with one name, mapped to one status code, is the ambiguity this "
            + "retirement removes");
    }

    private static bool Implements(Type candidate, Type retired)
    {
        if (retired.IsInterface)
        {
            return candidate.GetInterfaces().Any(
                @interface => @interface == retired
                    || (@interface.IsGenericType
                        && @interface.GetGenericTypeDefinition() == retired));
        }

        for (var current = candidate.BaseType; current is not null; current = current.BaseType)
        {
            if (current == retired
                || (current.IsGenericType && current.GetGenericTypeDefinition() == retired))
            {
                return true;
            }
        }

        return false;
    }
}
