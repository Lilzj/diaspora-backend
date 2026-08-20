using System.Reflection;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using Diaspora.Platform.Operations.Alerting;
using Diaspora.Platform.Operations.Configuration;
using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Policies;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The eight rules B17 adds, and the ones that keep the Production Platform from becoming the thing
/// it exists to prevent.
/// <para>
/// <b>Every operational capability on this platform is cross-cutting, and cross-cutting is a claim
/// that decays.</b> The first module to inject the circuit-breaker registry "just to check whether the
/// provider is up", the first domain rule that reads a feature flag, the first adapter with its own
/// retry loop — each is a small, reasonable-looking change that would pass review, and each removes a
/// property the whole phase was built to establish. These rules are what make the claim true a year
/// from now rather than on the day it shipped.
/// </para>
/// </summary>
public sealed class ProductionPlatformArchitectureTests
{
    private const string OperationsAssembly = "Diaspora.Operations";

    private static readonly IReadOnlyList<Assembly> PlatformAssemblies =
        ModuleAssemblyScanner.FindPlatformAssemblies();

    // ─── B17-R1. Business modules remain unaware of operational infrastructure ───────────

    /// <summary>
    /// <b>No business module references the Production Platform.</b> A module that can reach the
    /// circuit-breaker registry is a module that will one day open a circuit; one that can reach the
    /// cache implementation is one that will bypass its policy. The abstractions modules legitimately
    /// use — the cache port, the flag reader, the search seam — all live in <c>Diaspora.Platform</c>,
    /// which every module already references. Nothing a module needs is in this assembly.
    /// </summary>
    [Fact]
    public void No_business_module_references_the_operations_assembly()
    {
        var offenders = ModuleAssemblies()
            .Where(assembly => assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name == OperationsAssembly))
            .Select(assembly => assembly.GetName().Name!)
            .ToList();

        offenders.Should().BeEmpty(
            "the Production Platform is composed by the host and consumed through the kernel's " +
            "abstractions. A module that names it has an operational lever inside business logic, " +
            "and the modular boundary is gone with no compile error anywhere: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// The counterpart, and the one that would actually be tempting. <b>The Production Platform
    /// references no module.</b> Adapting a module's cache port, indexing a module's documents or
    /// verifying a module's audit trail all look like reasons to add one reference, and the result
    /// would be an assembly that has to be rebuilt whenever any of fifteen modules changes.
    /// </summary>
    [Fact]
    public void The_operations_assembly_references_no_module()
    {
        var operations = PlatformAssemblies.SingleOrDefault(
            assembly => assembly.GetName().Name == OperationsAssembly);

        operations.Should().NotBeNull("the Production Platform assembly should be in the scan");

        var offenders = operations!.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith("Diaspora.Modules.", StringComparison.Ordinal) == true)
            .ToList();

        offenders.Should().BeEmpty(
            "every operational capability is expressed as a port a module implements, never as a " +
            "reference the platform holds. A module's cache port is bridged at the composition root; " +
            "its documents reach the indexer through ISearchSynchronizer: " + string.Join(", ", offenders));
    }

    // ─── B17-R2. Operational services remain provider-independent ────────────────────────

    /// <summary>
    /// <b>No vendor is named in the Production Platform.</b> Redis, RabbitMQ, OpenSearch, Vault and
    /// every cloud provider are adapters this assembly has never heard of, and the moment one is named
    /// in a type, a using or a configuration key, "provider-independent" becomes a claim in a document
    /// rather than a property of the code.
    /// <para>
    /// Prose is exempt: naming what an adapter would be is how a seam explains itself, and a rule that
    /// forbade the word in a comment would be a rule that makes the code less clear to satisfy itself.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("StackExchange.Redis")]
    [InlineData("RabbitMQ")]
    [InlineData("OpenSearch")]
    [InlineData("Elasticsearch")]
    [InlineData("Amazon")]
    [InlineData("Azure")]
    [InlineData("Google.Cloud")]
    [InlineData("HashiCorp")]
    [InlineData("Hangfire")]
    [InlineData("Quartz")]
    [InlineData("Polly")]
    public void The_operations_assembly_names_no_vendor(string vendor)
    {
        var offenders = OperationsSource()
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(directive => directive.Name?.ToString()
                    .Contains(vendor, StringComparison.OrdinalIgnoreCase) == true)
                .Select(file.Locate))
            .ToList();

        offenders.Should().BeEmpty(
            $"'{vendor}' is an adapter the Production Platform registers, never one it references. " +
            "A vendor reference here makes every deployment carry it: " + string.Join(", ", offenders));
    }

    // ─── B17-R3. Feature flags do not leak into domain logic ─────────────────────────────

    /// <summary>
    /// <b>No domain type or handler reads a feature flag.</b> A flag inside domain logic makes the
    /// domain's behaviour depend on an operational lever: the same input produces different answers on
    /// two replicas mid-rollout, no test can pin the behaviour without also pinning the flag, and an
    /// invariant becomes conditional on a switch somebody can flip from a dashboard.
    /// <para>
    /// Flags belong at composition — choosing an implementation, mounting an endpoint, enabling a
    /// worker — where the choice is made once and is visible in one place.
    /// </para>
    /// </summary>
    [Fact]
    public void No_module_type_depends_on_the_feature_flag_service()
    {
        var offenders = ModuleAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter =>
                    parameter.ParameterType == typeof(IFeatureFlagService) ||
                    parameter.ParameterType == typeof(IFeatureFlagAdministration))
                .Select(parameter => $"{type.FullName}({parameter.ParameterType.Name})"))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "a feature flag inside a module is an operational lever inside business logic. The " +
            "platform's entitlement question — may this member do this — is IFeatureGateService and " +
            "is a different thing: " + string.Join(", ", offenders));
    }

    // ─── B17-R4. Resilience policies remain centralized ──────────────────────────────────

    /// <summary>
    /// <b>Retry lives in one place.</b> A retry loop written inside an adapter is invisible to the
    /// dashboard, uncoordinated with the breaker that is supposed to stop it, and untunable without a
    /// deployment. Worse, it multiplies: a handler that retries three times calling an adapter that
    /// retries three times makes nine calls to a dependency that is already failing.
    /// <para>
    /// Matched on the shape that gives it away — a <c>Task.Delay</c> inside a loop whose body also
    /// catches — rather than on the word "retry", because the loops that cause this are rarely named
    /// after it.
    /// </para>
    /// </summary>
    [Fact]
    public void No_module_writes_its_own_retry_loop()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles.Where(IsModuleFile))
        {
            foreach (var loop in file.Root.DescendantNodes()
                .Where(node => node is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax))
            {
                var delays = loop.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(invocation => invocation.Expression.ToString()
                        .EndsWith("Task.Delay", StringComparison.Ordinal));

                var catches = loop.DescendantNodes().OfType<CatchClauseSyntax>().Any();

                if (delays && catches)
                {
                    offenders.Add(file.Locate(loop));
                }
            }
        }

        offenders.Should().BeEmpty(
            "retry, backoff, timeout, bulkheading and circuit breaking are IResilienceExecutor's, " +
            "which reads them from the operational policy catalogue so a deployment can retune them " +
            "without a release. A hand-rolled loop is invisible to every one of those: " +
            string.Join(", ", offenders));
    }

    // ─── B17-R5. Telemetry remains cross-cutting ─────────────────────────────────────────

    /// <summary>
    /// <b>Every declared telemetry source exists.</b> The defect B2 shipped and B2.6 found: three
    /// activity source names registered with the tracer and no <c>ActivitySource</c> behind any of
    /// them, so the pipeline subscribed to silence and every trace was missing without an error
    /// anywhere. The contributor interface documents the obligation; this enforces it.
    /// </summary>
    [Fact]
    public void Every_declared_activity_source_and_meter_is_created_somewhere()
    {
        var declared = ModuleAssemblyScanner
            .CreateAll<IObservabilityContributor>(PlatformAssemblies)
            .SelectMany(contributor => contributor.ActivitySources.Concat(contributor.Meters))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        declared.Should().NotBeEmpty("the platform declares telemetry sources");

        // Resolution is per file, and that is the load-bearing part. Nearly every telemetry holder on
        // the platform is a class that declares `SourceName` and constructs its instruments from it —
        // seventeen classes, seventeen constants, all called `SourceName`, all with different values.
        // A repository-wide constant map resolves every one of them to whichever file was read last,
        // and the rule then reports sixteen modules as broken.
        //
        // Both creation syntaxes are matched too. Every holder writes
        // `static readonly Meter Meter = new(SourceName, Version)` — a target-typed `new`, which is an
        // ImplicitObjectCreationExpression and not an ObjectCreationExpression, so a rule that matched
        // only the explicit form finds nothing at all.
        var created = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ProductionFiles)
        {
            var constants = ConstantsIn(file);

            foreach (var creation in file.Root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(candidate => IsTelemetryType(candidate.Type.ToString())))
            {
                Record(created, FirstArgument(creation.ArgumentList), constants);
            }

            foreach (var creation in file.Root.DescendantNodes()
                .OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                // A target-typed `new` names no type, so the declared type of the field it
                // initialises is what identifies it.
                var declaration = creation.FirstAncestorOrSelf<VariableDeclarationSyntax>();

                if (declaration is not null && IsTelemetryType(declaration.Type.ToString()))
                {
                    Record(created, FirstArgument(creation.ArgumentList), constants);
                }
            }
        }

        var missing = declared.Where(name => !created.Contains(name)).ToList();

        missing.Should().BeEmpty(
            "a contributor that names a source nothing creates produces a tracer subscribed to " +
            "silence — the exact defect B2 shipped, which has no error message and no failing test " +
            "unless somebody writes this one: " + string.Join(", ", missing));
    }

    // ─── B17-R6. Health checks remain standardized ───────────────────────────────────────

    /// <summary>
    /// <b>A module reports health through the platform's contract, not ASP.NET's.</b> A module
    /// implementing <c>IHealthCheck</c> directly would have to know the tag strings the readiness
    /// endpoint filters on, would be unrunnable in a worker process with no web host, and would sit
    /// outside the aggregator's per-policy timeouts, caching and critical/degraded mapping — which is
    /// where the decision "does this failure take the instance out of rotation" actually lives.
    /// </summary>
    [Fact]
    public void No_module_implements_the_web_health_check_interface()
    {
        var offenders = ModuleAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                type.GetInterfaces().Any(contract =>
                    contract.FullName == "Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"))
            .Select(type => type.FullName!)
            .ToList();

        offenders.Should().BeEmpty(
            "implement IModuleHealthContributor instead. The host adapts it onto ASP.NET's pipeline " +
            "once, which is what gives every module's checks the same timeout, caching and " +
            "critical-versus-degraded semantics: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Every module health contributor names a module the registry knows. A contributor attributed to
    /// a name nobody recognises produces health results filed under a module that does not exist, and
    /// the dashboard shows them under a heading nobody can act on.
    /// </summary>
    [Fact]
    public void Every_health_contributor_names_a_real_module()
    {
        var known = new ModuleRegistry().DiscoverModules(PlatformAssemblies).Modules
            .Select(registration => registration.Name)
            .Append("Platform")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var offenders = ModuleAssemblyScanner
            .FindImplementations<IModuleHealthContributor>(PlatformAssemblies)
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (IModuleHealthContributor)Activator.CreateInstance(type)!)
            .Where(contributor => !known.Contains(contributor.Module))
            .Select(contributor => contributor.Module)
            .ToList();

        offenders.Should().BeEmpty(
            "a health contributor filed under an unknown module produces results nobody can act on: " +
            string.Join(", ", offenders));
    }

    // ─── B17-R7. Caching remains abstraction-based ───────────────────────────────────────

    /// <summary>
    /// <b>No module touches a cache implementation.</b> <c>IMemoryCache</c> and
    /// <c>IDistributedCache</c> in a module are a cache with no policy, no tags, no invalidation and no
    /// metric — which is the state three modules were in before B17, each for a defensible reason, and
    /// which is exactly what a platform cache exists to end.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.Extensions.Caching.Memory")]
    [InlineData("Microsoft.Extensions.Caching.Distributed")]
    public void No_module_uses_a_cache_implementation_directly(string forbidden)
    {
        var offenders = SourceTree.ProductionFiles
            .Where(IsModuleFile)
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(directive => directive.Name?.ToString() == forbidden)
                .Select(file.Locate))
            .ToList();

        offenders.Should().BeEmpty(
            $"'{forbidden}' is a cache with no policy, no tags, no invalidation and no metric. " +
            "Depend on ICacheService and name a CachePolicy: " + string.Join(", ", offenders));
    }

    // ─── B17-R8. Configuration remains validated ─────────────────────────────────────────

    /// <summary>
    /// <b>Every declared runtime setting can be checked.</b> A setting whose declaration constrains
    /// nothing and has no validator accepts any string — so a typo becomes a live value, the reader
    /// parses it to zero or false, and the platform behaves differently for a reason nothing reports.
    /// <para>
    /// A <c>Text</c> setting with no constraint is the one legitimate case and is exempt: some settings
    /// genuinely are free text.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_declared_runtime_setting_is_constrained_or_free_text()
    {
        var validators = ModuleAssemblyScanner
            .FindImplementations<IConfigurationValidator>(PlatformAssemblies)
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => ((IConfigurationValidator)Activator.CreateInstance(type)!).Key)
            .ToHashSet(StringComparer.Ordinal);

        var offenders = ModuleAssemblyScanner
            .FindImplementations<IRuntimeConfigurationContributor>(PlatformAssemblies)
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (IRuntimeConfigurationContributor)Activator.CreateInstance(type)!)
            .SelectMany(contributor => contributor.Settings)
            .Where(setting =>
                setting.Kind is not ConfigurationValueKind.Text &&
                setting.AllowedValues is not { Count: > 0 } &&
                setting.Minimum is null &&
                setting.Maximum is null &&
                !validators.Contains(setting.Key))
            .Select(setting => setting.Key)
            .ToList();

        offenders.Should().BeEmpty(
            "a numeric, duration or list setting with no bound and no validator accepts anything, " +
            "and a bad value becomes a silent behaviour change: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Every operational policy the platform composes passes its own validation. The catalogue already
    /// refuses to compose an invalid one at startup — this is the same check run without a host, so a
    /// bad default fails a unit-speed test rather than every integration test at once.
    /// </summary>
    [Fact]
    public void Every_composed_operational_policy_is_valid()
    {
        var offenders = ModuleAssemblyScanner
            .FindImplementations<IOperationalPolicyContributor>(PlatformAssemblies)
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (IOperationalPolicyContributor)Activator.CreateInstance(type)!)
            .SelectMany(contributor => contributor.Policies
                .SelectMany(policy => policy.Validate().Select(problem => $"{policy}: {problem}")))
            .ToList();

        offenders.Should().BeEmpty(
            "an invalid policy stops the host at composition, which means every integration test " +
            "fails at once with one message: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// Every alert rule has a runbook. An alert nobody knows how to answer gets acknowledged, then
    /// ignored, then muted — and the platform ends up with a monitoring system that nobody trusts and
    /// a set of conditions nobody watches.
    /// </summary>
    [Fact]
    public void Every_alert_rule_carries_a_runbook_and_a_window()
    {
        var rules = ModuleAssemblyScanner
            .FindImplementations<IAlertRuleContributor>(PlatformAssemblies)
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (IAlertRuleContributor)Activator.CreateInstance(type)!)
            .SelectMany(contributor => contributor.Rules)
            .ToList();

        rules.Should().NotBeEmpty("the platform declares alert rules");

        var offenders = rules
            .Where(rule => string.IsNullOrWhiteSpace(rule.Runbook) || rule.For <= TimeSpan.Zero)
            .Select(rule => rule.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "a rule with no stated response, or one that fires the instant a threshold is crossed, " +
            "trains people to ignore it: " + string.Join(", ", offenders));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────────────

    private static IEnumerable<Assembly> ModuleAssemblies() =>
        PlatformAssemblies.Where(assembly =>
            assembly.GetName().Name?.StartsWith("Diaspora.Modules.", StringComparison.Ordinal) == true);

    private static bool IsModuleFile(SourceFile file) =>
        file.Path.StartsWith("src/Diaspora.Modules.", StringComparison.Ordinal);

    private static bool IsTelemetryType(string type) =>
        type.EndsWith("ActivitySource", StringComparison.Ordinal) ||
        type.EndsWith("Meter", StringComparison.Ordinal);

    private static string FirstArgument(ArgumentListSyntax? arguments) =>
        arguments?.Arguments.FirstOrDefault()?.ToString() ?? string.Empty;

    /// <summary>
    /// Constant and static-readonly fields declared in one file, by member name.
    /// <para>
    /// Per file rather than repository-wide, because the name a telemetry class gives its own constant
    /// is nearly always <c>SourceName</c> and seventeen classes use it for seventeen different values.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> ConstantsIn(SourceFile file)
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in file.Root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(modifier =>
                modifier.IsKind(SyntaxKind.ConstKeyword) || modifier.IsKind(SyntaxKind.StaticKeyword))))
        {
            foreach (var declarator in field.Declaration.Variables
                .Where(declarator => declarator.Initializer is not null))
            {
                constants[declarator.Identifier.Text] = declarator.Initializer!.Value.ToString();
            }
        }

        return constants;
    }

    private static void Record(
        HashSet<string> created, string argument, IReadOnlyDictionary<string, string> constants)
    {
        var value = Resolve(argument, constants);

        if (value.Length > 0)
        {
            created.Add(value);
        }
    }

    private static IEnumerable<SourceFile> OperationsSource() =>
        SourceTree.ProductionFiles.Where(
            file => file.Path.StartsWith("src/Diaspora.Operations/", StringComparison.Ordinal));

    /// <summary>
    /// Turns a source argument into the string it evaluates to — a literal directly, or a constant
    /// resolved through the declarations this suite already parsed.
    /// <para>
    /// Two lookups, in order. An unqualified name resolves against the file's own constants, because
    /// <c>SourceName</c> means seventeen different things in seventeen files. A qualified one —
    /// <c>BookmarkTelemetry.SourceName</c>, <c>PlatformTelemetryNames.OperationsActivitySource</c> —
    /// resolves against the repository, keyed by owning type and member together, which is exactly as
    /// specific as the source that wrote it.
    /// </para>
    /// </summary>
    private static string Resolve(string argument, IReadOnlyDictionary<string, string> fileConstants)
    {
        var value = argument.Trim();

        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
        {
            return value[1..^1];
        }

        var separator = value.LastIndexOf('.');

        if (separator < 0)
        {
            return fileConstants.TryGetValue(value, out var local)
                ? Resolve(local, fileConstants)
                : string.Empty;
        }

        // The last two segments: "Namespace.Type.Member" and "Type.Member" both key the same entry.
        var qualified = value[..separator].LastIndexOf('.') is var owner && owner >= 0
            ? value[(owner + 1)..]
            : value;

        return QualifiedConstants.Value.TryGetValue(qualified, out var declared)
            ? Resolve(declared, fileConstants)
            : string.Empty;
    }

    /// <summary>
    /// Every constant in the repository, keyed by <c>OwningType.Member</c>.
    /// <para>
    /// Qualified because the bare names collide by design — a telemetry class naming its own constant
    /// <c>SourceName</c> is the convention, not an accident.
    /// </para>
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> QualifiedConstants = new(() =>
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var field in file.Root.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(field => field.Modifiers.Any(modifier =>
                    modifier.IsKind(SyntaxKind.ConstKeyword) || modifier.IsKind(SyntaxKind.StaticKeyword))))
            {
                var owner = field.FirstAncestorOrSelf<TypeDeclarationSyntax>()?.Identifier.Text;

                if (owner is null)
                {
                    continue;
                }

                foreach (var declarator in field.Declaration.Variables
                    .Where(declarator => declarator.Initializer is not null))
                {
                    constants[$"{owner}.{declarator.Identifier.Text}"] =
                        declarator.Initializer!.Value.ToString();
                }
            }
        }

        return constants;
    });

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
