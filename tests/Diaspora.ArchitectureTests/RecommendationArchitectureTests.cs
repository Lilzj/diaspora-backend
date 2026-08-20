using System.Reflection;
using System.Runtime.CompilerServices;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Recommendations;
using Diaspora.Modules.Recommendations.Application.Consumers;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Policies;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Recommendation Platform must not cross, enforced rather than documented.
/// <para>
/// Each of these is true on the day it is written and quietly false two releases later unless
/// something checks. The reference test is the one that earns its keep: a single
/// <c>ProjectReference</c> added to read one extra field would undo the module's central claim, and
/// it would pass code review as a convenience.
/// </para>
/// </summary>
public class RecommendationArchitectureTests
{
    private static readonly Assembly Module = typeof(RecommendationsModule).Assembly;

    /// <summary>
    /// <b>The Recommendation Platform owns no business data, and this is how that is checked.</b> It
    /// references no vertical at all — not Jobs, not Housing, not Trust, not even Identity. Every
    /// recommendation is assembled from a published contract's payload projected into the candidate
    /// table; if the payload is not enough, the fix is to enrich the contract, never to reach into
    /// the module.
    /// </summary>
    [Fact]
    public void The_module_references_no_other_module()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Trust",
                "Diaspora.Modules.Location",
                "Diaspora.Modules.Media",
                "Diaspora.Modules.Community",
                "Diaspora.Modules.Providers",
                "Diaspora.Modules.Guides",
                "Diaspora.Modules.Jobs",
                "Diaspora.Modules.Marketplace",
                "Diaspora.Modules.Housing",
                "Diaspora.Modules.CommunityLife",
                "Diaspora.Modules.Notifications",
                "Diaspora.Modules.Bookmarks",
                "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Recommendation Platform consumes published contracts and nothing else. Offenders: "
            + Describe(result));
    }

    /// <summary>
    /// <b>Only integration events cross a module boundary.</b> Every consumer derives from
    /// <see cref="RecommendationEventConsumer{T}"/>, which derives from
    /// <c>IntegrationEventHandler</c> — so a consumer written against the domain pipeline is a
    /// compile error rather than a silent bypass of the outbox.
    /// </summary>
    [Fact]
    public void Every_cross_module_consumer_is_an_integration_event_handler()
    {
        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.Contains(".Application.Consumers", StringComparison.Ordinal) == true)
            .Where(type => type is { IsAbstract: false, IsInterface: false })

            // Iterator and async methods compile to nested state-machine types in the same
            // namespace. They are not consumers, and a rule that counted them would fail on every
            // consumer that awaits — which is most of them.
            .Where(type => !IsCompilerGenerated(type))

            // The content-type map is a translation table in the consumers' namespace because that
            // is the only place it is used. It is static and consumes nothing.
            .Where(type => !type.IsAbstract || !type.IsSealed)
            .Where(type => !IsIntegrationEventConsumer(type))
            .ToList();

        offenders.Should().BeEmpty(
            "a consumer of another module's fact must ride the integration pipeline. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>Business modules never compute recommendations.</b> There is exactly one engine, and it
    /// lives here. A second implementation anywhere would be a path where blocked content, muted
    /// categories and a personalization opt-out are not enforced — because the enforcement is only as
    /// good as the number of ways around it.
    /// </summary>
    [Fact]
    public void Only_one_type_implements_the_engine()
    {
        var implementations = Module.GetTypes()
            .Where(type => typeof(IRecommendationEngine).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToList();

        implementations.Should().ContainSingle(
            "one door into a recommendation is what makes the policy pass unavoidable");
    }

    /// <summary>
    /// <b>Recommendation policies remain separate from ranking strategies.</b> The brief asks for it
    /// and the only way to make it stick is to make the combination a build failure — because the
    /// pressure to merge them is constant and always reasonable-sounding. A demotion looks like a
    /// negative weight and an exclusion looks like a weight of zero, and neither is true: a
    /// zero-weighted candidate still appears when nothing else does, and blocked content must never
    /// appear however empty the page is.
    /// </summary>
    [Fact]
    public void No_type_is_both_a_ranking_strategy_and_a_policy()
    {
        var offenders = Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IRankingStrategy).IsAssignableFrom(type) &&
                           typeof(IRecommendationPolicyRule).IsAssignableFrom(type))
            .ToList();

        offenders.Should().BeEmpty(
            "a strategy scores and a policy decides; a type that did both would let an exclusion be "
            + "expressed as a low score. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>Every recommendation includes an explanation, and the type system is what guarantees it.</b>
    /// <see cref="RecommendationItem"/> has no factory that omits the explanation — so a reviewer
    /// cannot forget it, a refactor cannot drop it, and a future ranking adapter cannot skip it.
    /// </summary>
    [Fact]
    public void A_recommendation_item_cannot_be_created_without_an_explanation()
    {
        var factories = typeof(RecommendationItem)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(RecommendationItem))
            .ToList();

        factories.Should().NotBeEmpty("the item is created through a factory rather than a constructor");

        factories.Should().OnlyContain(
            method => method.GetParameters().Any(
                parameter => parameter.ParameterType.Name == "RecommendationExplanation"),
            "an unexplained recommendation is the one outcome this module exists to prevent, and a "
            + "factory that omitted the explanation would make it expressible");

        typeof(RecommendationItem)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Should()
            .BeEmpty("the factory is the only way to build one");
    }

    /// <summary>
    /// <b>Ranking strategies remain provider-independent.</b> No strategy, similarity provider or
    /// candidate source may name a vendor. The check is deliberately crude — a substring match on
    /// well-known names — because the failure it guards against is somebody adding
    /// <c>OpenAiReranker</c> to this assembly rather than to an adapter package.
    /// </summary>
    [Fact]
    public void No_ranking_component_names_a_vendor()
    {
        string[] vendors =
        [
            "OpenAi", "Anthropic", "Claude", "Gpt", "Llama", "Gemini", "Cohere", "HuggingFace",
            "Pinecone", "Weaviate", "Qdrant", "Milvus", "Chroma", "Elasticsearch", "OpenSearch",
            "Algolia", "Redis", "TensorFlow", "PyTorch", "Onnx", "Sagemaker", "Vertex"
        ];

        var offenders = Module.GetTypes()
            .Where(type => !IsCompilerGenerated(type))
            .Where(type => vendors.Any(vendor =>
                type.Name.Contains(vendor, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        offenders.Should().BeEmpty(
            "the shipped ranking is deterministic and vendor-free; an adapter belongs in its own "
            + "assembly. Offenders: " + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>AI providers remain optional adapters.</b> The three machine-learning strategy kinds are
    /// declared and nothing in this assembly implements them — which is what makes them a seam rather
    /// than a plan. If one ever gains an implementation here, this test fails and the module has
    /// quietly acquired a dependency it was built to avoid.
    /// </summary>
    [Fact]
    public void No_machine_learning_strategy_ships()
    {
        var implemented = Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(IRankingStrategy).IsAssignableFrom)
            .Select(type => (IRankingStrategy)Activator.CreateInstance(type, NonPublicArguments(type))!)
            .Where(strategy => strategy.Kind is
                Modules.Recommendations.Domain.Enums.RankingStrategyKind.MachineLearning or
                Modules.Recommendations.Domain.Enums.RankingStrategyKind.VectorSimilarity or
                Modules.Recommendations.Domain.Enums.RankingStrategyKind.LanguageModel)
            .ToList();

        implemented.Should().BeEmpty(
            "the AI kinds are extension points; shipping one here would make the module depend on "
            + "what it was built to keep optional");
    }

    /// <summary>
    /// <b>The module's URL space is declared in one place.</b> A second prefix appearing elsewhere
    /// would be a surface nobody knew existed, and the route prefix is the only thing standing
    /// between "one module, one namespace" and a URL space that grows by accident.
    /// </summary>
    [Fact]
    public void The_module_maps_one_route_prefix()
    {
        var contributors = Module.GetTypes()
            .Where(type => typeof(IEndpointContributor).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToList();

        contributors.Should().ContainSingle("one module, one URL space");

        RecommendationsModuleMetadata.RoutePrefix.Should().Be("/api/v1/recommendations");
    }

    /// <summary>
    /// <b>The domain layer does not reference EF Core.</b> The platform rule, checked here for this
    /// module because the aggregates carry query-shaped concepts — filters, cursors, projections —
    /// and are the likeliest place for a persistence type to leak upward.
    /// </summary>
    [Fact]
    public void The_domain_layer_does_not_reference_entity_framework()
    {
        var result = Types.InAssembly(Module)
            .That()
            .ResideInNamespaceStartingWith("Diaspora.Modules.Recommendations.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain describes what is true, not how it is stored. Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>The extensibility layer does not reference the infrastructure that implements it.</b> A
    /// seam that knew its own default is not a seam — the dependency would run backwards, and an
    /// adapter replacing the default would find the abstraction already coupled to what it replaced.
    /// </summary>
    [Fact]
    public void The_extensibility_layer_does_not_reference_its_implementations()
    {
        var result = Types.InAssembly(Module)
            .That()
            .ResideInNamespaceStartingWith("Diaspora.Modules.Recommendations.Extensibility")
            .Should()
            .NotHaveDependencyOn("Diaspora.Modules.Recommendations.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a seam that knows its own default is not a seam. Offenders: " + Describe(result));
    }

    private static bool IsIntegrationEventConsumer(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(IntegrationEventHandler<>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Constructor arguments for a strategy, as nulls.
    /// <para>
    /// The strategies under test read only their own <c>Kind</c>, which is a constant, so nulls are
    /// sufficient and building a container for an architecture test would be worse than the
    /// alternative.
    /// </para>
    /// </summary>
    private static object?[] NonPublicArguments(Type type)
    {
        var constructor = type.GetConstructors().FirstOrDefault();

        return constructor is null
            ? []
            : constructor.GetParameters().Select(_ => (object?)null).ToArray();
    }

    private static bool IsCompilerGenerated(Type type) =>
        type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null ||
        type.Name.Contains('<', StringComparison.Ordinal);

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null
            ? "none reported"
            : string.Join(", ", result.FailingTypeNames);
}
