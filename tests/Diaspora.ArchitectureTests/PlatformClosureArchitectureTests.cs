using System.Reflection;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Conversations;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Recommendations;
using Diaspora.Modules.Trust;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// PSR-1.2's rules: the seams this phase connected, and the boundaries that made connecting them
/// safe.
/// <para>
/// Same standard as PSR-1.1's set — every rule here forbids something the phase found already
/// happening, or pins a join the phase built after finding the other half missing. A rule written
/// against a hypothetical is one nobody trusts.
/// </para>
/// </summary>
public sealed class PlatformClosureArchitectureTests
{
    private static readonly Assembly Contracts = typeof(IIntegrationEvent).Assembly;

    private static readonly Assembly ConversationsModule = typeof(ConversationsModuleMetadata).Assembly;

    private static readonly Assembly TrustModule = typeof(TrustModuleMetadata).Assembly;

    // ── Conversation ↔ Trust ─────────────────────────────────────────────

    /// <summary>
    /// <b>The conversation policy seam has production implementations.</b> B13.2 shipped
    /// <see cref="IConversationPolicy"/> consulted on every conversation opened, with nothing behind
    /// it, and recorded the consequence as a deployment's problem: <em>"Trust's account-level bans do
    /// not currently prevent opening a conversation; per-conversation blocking does."</em> A member
    /// the platform had suspended could not post, list, comment or review, and could still open a
    /// thread with anybody.
    /// </summary>
    [Fact]
    public void The_conversation_policy_seam_is_implemented()
    {
        Implementations<IConversationPolicy>(ConversationsModule).Should().NotBeEmpty(
            "a refusal seam consulted on every write, with nothing behind it, is a rule nobody "
            + "wrote down and nobody enforces");
    }

    /// <summary>
    /// <b>Conversation policy belongs to Conversations.</b> The seam is scanned across every platform
    /// assembly, so Trust could have shipped the implementation — and would then reference a module
    /// composed thirteen hundred places after it, invert the module order, and own a decision about
    /// what a conversation is. Trust states a standing; the consuming module decides what it stops.
    /// </summary>
    [Fact]
    public void No_other_module_implements_the_conversation_policy_seam()
    {
        foreach (var assembly in PlatformModuleAssemblies().Where(a => a != ConversationsModule))
        {
            Implementations<IConversationPolicy>(assembly).Should().BeEmpty(
                $"{assembly.GetName().Name} would be deciding who may open a conversation");
        }
    }

    /// <summary>
    /// <b>And it reads Trust through the kernel, not through a reference.</b> The whole arrangement
    /// depends on this: the policies ask <see cref="ITrustService"/>, which the Trust module takes
    /// over at composition, so the two modules are joined by a contract in the kernel and by nothing
    /// else. A project reference here would be a circular dependency waiting for the first time
    /// Trust wanted to know something about a conversation.
    /// </summary>
    [Fact]
    public void Conversations_holds_no_reference_to_the_trust_module()
    {
        ConversationsModule.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain("Diaspora.Modules.Trust");
    }

    /// <summary>The other direction, which is the one that would be circular.</summary>
    [Fact]
    public void Trust_holds_no_reference_to_the_conversations_module()
    {
        TrustModule.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain("Diaspora.Modules.Conversations");
    }

    // ── Kernel ports ─────────────────────────────────────────────────────

    /// <summary>
    /// <b>A kernel service port whose only implementation is the kernel's own placeholder is a
    /// question the platform cannot ask.</b>
    /// <para>
    /// <c>IEligibilityService</c> was exactly that for four years of phases:
    /// <c>DefaultEligibilityService</c> answered <c>Eligible</c> to everything while the Trust module
    /// held the real evaluator and served it over HTTP. **It was invisible because a port
    /// <em>with</em> an implementation passes every dormant-port rule the platform has** —
    /// <c>No_kernel_port_is_dormant</c>, <c>No_module_depends_on_a_dormant_kernel_port</c> and
    /// <c>DormantPortTests</c> all ask *is there an implementation*, and none asks *whose*.
    /// </para>
    /// <para>
    /// <b>What identifies a placeholder, precisely.</b> The kernel's stubs are the <c>Default…</c>
    /// types in <c>Diaspora.Infrastructure.Services</c> — one folder, one naming convention, and
    /// every one of them documented as deliberately and visibly wrong. A port whose only
    /// implementation is one of those has nobody behind it. That is narrower and truer than "only
    /// implemented by an infrastructure assembly": <c>ICacheService</c> and
    /// <c>IBackgroundJobScheduler</c> are implemented in <c>Diaspora.Operations</c> and
    /// <c>IFeatureGateService</c> in <c>Diaspora.Infrastructure.Identity</c>, and all three are real
    /// owners rather than stubs.
    /// </para>
    /// <para>
    /// The exemption list is the ports for which the placeholder <em>is</em> the final answer — each
    /// reads the ambient request or writes to a sink, and no module knows more about any of them.
    /// A port leaving that list is a module claiming it, which is the change this rule exists to
    /// force somebody to make on purpose.
    /// </para>
    /// </summary>
    [Fact]
    public void No_kernel_port_is_answered_only_by_a_placeholder()
    {
        // Ports whose only correct home is the composition root. Each has a Default… implementation
        // and that is the whole implementation there will ever be: an audit sink, the correlation
        // id, the ambient request. Two of the five are additionally replaced at runtime by an
        // HttpContext-reading implementation in the host.
        string[] infrastructureOwned =
        [
            nameof(IAuditService),
            nameof(ICorrelationContext),
            nameof(ICurrentUser),
            nameof(IPermissionService),
            nameof(IRequestContext)
        ];

        const string PlaceholderNamespace = "Diaspora.Infrastructure.Services";

        var kernelPorts = typeof(ITrustService).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.IsPublic)
            .Where(type => type.Namespace == "Diaspora.Platform.Services.Abstractions")
            .Where(type => !infrastructureOwned.Contains(type.Name))
            .ToList();

        kernelPorts.Should().NotBeEmpty("the rule must have something to check");

        var implementations = PlatformAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToList();

        var offenders = new List<string>();

        foreach (var port in kernelPorts)
        {
            var implementors = implementations.Where(port.IsAssignableFrom).ToList();

            // No implementation at all is a different rule's business — No_kernel_port_is_dormant
            // already owns it, and the deliberately obsoleted port is exempt there rather than here.
            if (implementors.Count == 0)
            {
                continue;
            }

            if (implementors.TrueForAll(type =>
                    type.Namespace == PlaceholderNamespace
                    && type.Name.StartsWith("Default", StringComparison.Ordinal)))
            {
                offenders.Add($"{port.Name} (only {string.Join(", ", implementors.Select(t => t.Name))})");
            }
        }

        offenders.Should().BeEmpty(
            "a kernel port answered only by its placeholder reads as available and is not — which is "
            + "how IEligibilityService returned Eligible for every action for four years while the "
            + "Trust module computed the real answer three feet away. Either the module that owns "
            + "the question takes the port over, or the port joins the reviewed infrastructure list "
            + "above with a reason. Offenders: " + string.Join("; ", offenders));
    }

    // ── The Conversations integrations this phase made ───────────────────

    /// <summary>
    /// <b>Recommendation consumes the conversation contract that carries intent.</b> B13.2's event
    /// catalogue has a <c>Consumed by</c> column and it names this module against four contracts;
    /// none was built. <c>ConversationCreatedIntegrationEvent</c> is the one the payload already
    /// supports — it carries the context type, the entity and the initiator, which is everything an
    /// affinity signal needs.
    /// </summary>
    [Fact]
    public void Recommendation_consumes_the_conversation_intent_contract()
    {
        ConsumedBy(
            typeof(RecommendationsModuleMetadata).Assembly,
            "ConversationCreatedIntegrationEvent",
            "Diaspora.Contracts.Conversations.V1.Events")
            .Should().BeTrue(
                "enquiring about a listing is the third of the three things SignalKind.Applied "
                + "names, and it was the one nothing produced");
    }

    /// <summary>
    /// The same property PSR-1.1 pinned for Financial: the consumer binds to a published contract
    /// and takes no reference on the module that publishes it.
    /// </summary>
    [Fact]
    public void Recommendation_holds_no_reference_to_the_conversations_module()
    {
        typeof(RecommendationsModuleMetadata).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain("Diaspora.Modules.Conversations");
    }

    // ── The gateway ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>The route prefix question is closed centrally, and must stay closed centrally.</b> The
    /// client addresses <c>/v1/{module}</c> and every module serves <c>/api/v1/{module}</c>. B13.2
    /// declined to move one module onto a different prefix and was right to: the only wrong way to
    /// fix this is one module at a time. Every declared prefix still begins with the canonical root.
    /// </summary>
    [Fact]
    public void Every_module_still_declares_the_canonical_route_prefix()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.EndsWith("ModuleMetadata.cs", StringComparison.Ordinal)))
        {
            foreach (var declarator in file.Root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(node => node.Identifier.ValueText.EndsWith("RoutePrefix", StringComparison.Ordinal)))
            {
                var value = declarator.Initializer?.Value.ToString().Trim('"');

                if (value is not null && !value.StartsWith("/api/v1/", StringComparison.Ordinal))
                {
                    offenders.Add($"{file.Locate(declarator)} = {value}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a module on its own prefix is the failure the compatibility rewrite exists to prevent, "
            + "not the fix for it. Offenders: " + string.Join("; ", offenders));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static List<Type> Implementations<TSeam>(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(TSeam).IsAssignableFrom(type))
            .ToList();

    private static IEnumerable<Assembly> PlatformModuleAssemblies() =>
        PlatformAssemblies()
            .Where(assembly =>
                assembly.GetName().Name?.StartsWith("Diaspora.Modules.", StringComparison.Ordinal) == true);

    private static IEnumerable<Assembly> PlatformAssemblies() =>
        Diaspora.Platform.Modules.ModuleAssemblyScanner.FindPlatformAssemblies()
            .Where(assembly =>
                assembly.GetName().Name?.EndsWith("Tests", StringComparison.Ordinal) != true);

    private static bool ConsumedBy(Assembly assembly, string contractName, string contractNamespace)
    {
        var contract = Contracts.GetTypes()
            .SingleOrDefault(type => type.Name == contractName && type.Namespace == contractNamespace);

        contract.Should().NotBeNull($"{contractName} must exist to be consumed");

        return assembly.GetTypes().Any(type =>
            !type.IsAbstract
            && Ancestry(type).Any(ancestor =>
                ancestor.IsGenericType && ancestor.GetGenericArguments().Contains(contract)));
    }

    private static IEnumerable<Type> Ancestry(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }
}
