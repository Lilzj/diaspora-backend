using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Modules;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// CQRS, persistence, API, recommendation, communication and AI-readiness guardrails — the parts of
/// the brief whose rules are one or two assertions each rather than a family.
/// <para>
/// <b>Grouped by what a violation would cost rather than by which layer it lives in.</b> Every rule
/// below is a lesson some earlier phase paid for: a command that stages a write and never commits
/// it, a restore that cannot see the row it is restoring, two modules claiming one URL space, a
/// recommendation with no reason attached, a vertical deciding to notify somebody itself.
/// </para>
/// </summary>
public sealed class PlatformGuardrailArchitectureTests
{
    private static readonly IReadOnlyList<Assembly> Platform =
        ModuleAssemblyScanner.FindPlatformAssemblies();

    // ── CQRS ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A query is safe to retry, to cache, to run against a replica and to reach from a GET —
    /// or it says out loud that it is not.</b> Four handlers on this platform record the view that
    /// the read caused, deliberately, and each now carries <c>[SideEffecting]</c> with the reason.
    /// The attribute is the difference between an exception somebody chose and one nobody noticed.
    /// </summary>
    [Fact]
    public void Every_query_handler_that_writes_declares_the_side_effect()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.EndsWith("QueryHandler.cs", StringComparison.Ordinal)))
        {
            var writes = file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "SaveChangesAsync"
                    });

            if (writes && !file.Root.ToString().Contains("[SideEffecting", StringComparison.Ordinal))
            {
                offenders.Add(file.Path);
            }
        }

        offenders.Should().BeEmpty(
            "move the write to a command, or declare it with [SideEffecting]:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The defect this rule found in B4.</b> <c>TransactionBehavior</c> reads
    /// <c>[Transactional]</c> off <c>TRequest</c> — the command — because that is the type MediatR
    /// closes the pipeline over. Location's <c>DeclareCountryExperienceCommandHandler</c> carried
    /// the attribute on the handler, with a class comment asserting a transaction, and no
    /// transaction was ever opened. That is B2's logging-only transaction behaviour again, in one
    /// class, four phases after it was supposedly fixed.
    /// </summary>
    [Fact]
    public void Transactional_appears_only_on_commands()
    {
        var offenders = Platform
            .SelectMany(Loadable)
            .Where(type => type.GetCustomAttribute<TransactionalAttribute>() is not null)
            .Where(type => !type.GetInterfaces().Any(contract =>
                contract == typeof(ICommand) ||
                (contract.IsGenericType &&
                 contract.GetGenericTypeDefinition() == typeof(ICommand<>))))
            .Select(type => type.FullName!)
            .ToList();

        offenders.Should().BeEmpty(
            "on anything but a command the attribute compiles, reads as a guarantee and opens " +
            "nothing:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// The counterpart, so the rule above cannot be satisfied by deleting every usage.
    /// </summary>
    [Fact]
    public void The_platform_still_marks_its_multi_write_commands()
    {
        var transactional = Platform
            .SelectMany(Loadable)
            .Count(type => type.GetCustomAttribute<TransactionalAttribute>() is not null);

        transactional.Should().BeGreaterThan(
            50, "the platform has dozens of genuinely multi-write commands and they are all marked");
    }

    // ── Persistence: the undelete blind spot ─────────────────────────────

    /// <summary>
    /// <b>The blind spot, and it is exactly one line wide.</b> A soft-deleted row is hidden by a
    /// global query filter, so the ordinary retrieval cannot see it — and a restore command that
    /// loads through the ordinary retrieval answers "not found" for every row it exists to bring
    /// back. Every handler that calls the parameterless <c>Restore()</c> — which is
    /// <see cref="ISoftDeletable"/>'s, as opposed to the several aggregates whose <c>Restore</c>
    /// takes an argument and moves a status — must load through a retrieval that ignores the filter.
    /// </summary>
    [Fact]
    public void Every_undelete_loads_through_a_filter_ignoring_retrieval()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            var undeletes = file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.ArgumentList.Arguments.Count == 0 &&
                    invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "Restore"
                    });

            if (!undeletes)
            {
                continue;
            }

            var bypasses = file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax member &&
                    member.Name.Identifier.ValueText.Contains("Including", StringComparison.Ordinal));

            if (!bypasses)
            {
                offenders.Add(file.Path);
            }
        }

        offenders.Should().BeEmpty(
            "the global query filter hides the row this handler exists to bring back:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// And the retrievals named for it really do it. A method called
    /// <c>GetIncludingDeletedAsync</c> that forgot <c>IgnoreQueryFilters</c> is worse than one that
    /// does not exist: every caller believes the blind spot is covered.
    /// </summary>
    [Fact]
    public void Every_filter_ignoring_retrieval_actually_ignores_the_filter()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("Repositories", StringComparison.Ordinal)))
        {
            foreach (var method in file.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method =>
                    method.Identifier.ValueText.Contains("Including", StringComparison.Ordinal)))
            {
                if (!method.ToString().Contains("IgnoreQueryFilters", StringComparison.Ordinal))
                {
                    offenders.Add($"{file.Locate(method)} — {method.Identifier.ValueText}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "the name promises the filter is bypassed:\n" + string.Join("\n", offenders));
    }

    // ── API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every module's URL space is declared once, versioned, and owned by one module.</b> The
    /// version lives in the prefix because a breaking change to a module's contracts is a new
    /// prefix rather than a negotiation, and two modules claiming one prefix is an ambiguous route
    /// that ASP.NET Core reports at request time — on the request, in production.
    /// </summary>
    [Fact]
    public void Every_declared_route_prefix_is_versioned_and_owned_by_one_module()
    {
        var prefixes = RoutePrefixes();

        // Thirteen modules contribute endpoints and Guides contributes two groups — its own and
        // B8.1's knowledge directory. Since B1.4.1 closed F6 every one of them is a constant, so
        // this floor is the count rather than an allowance.
        prefixes.Should().HaveCountGreaterThanOrEqualTo(
            14, "every module that contributes endpoints declares its URL space as a constant");

        foreach (var (owner, prefix) in prefixes)
        {
            prefix.Should().MatchRegex(
                "^/api/v[0-9]+/[a-z0-9-]+$",
                "{0}'s prefix must be lower case, versioned and one segment deep", owner);
        }

        prefixes.Select(entry => entry.Prefix)
            .Should().OnlyHaveUniqueItems("two modules cannot own one URL space");
    }

    /// <summary>
    /// <b>The rule above is only as wide as the constants it can read, and B1.4 found that out.</b>
    /// Bookmarks spelled <c>/api/v1/bookmarks</c> as a literal in its endpoint contributor and
    /// declared no prefix constant, so it routed correctly and was invisible to every rule about
    /// prefixes — recorded as F6 and closed in B1.4.1. A module can take a URL space back out of
    /// scope the same way at any time, so the rule that matters is this one: the group's prefix
    /// comes from a constant, not from a string in the middle of a method.
    /// </summary>
    [Fact]
    public void Every_endpoint_contributor_maps_its_group_from_a_declared_constant()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.EndsWith("EndpointContributor.cs", StringComparison.Ordinal)))
        {
            foreach (var invocation in file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "MapGroup"
                    }))
            {
                var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;

                if (argument is not MemberAccessExpressionSyntax and not IdentifierNameSyntax)
                {
                    offenders.Add($"{file.Locate(invocation)} — {argument}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a literal prefix is a URL space no rule about prefixes can see:\n" +
            string.Join("\n", offenders));
    }

    // ── Recommendations ──────────────────────────────────────────────────

    /// <summary>
    /// <b>B14's central promise, kept structurally.</b> Every recommendation carries a reason, a
    /// score, the strategy that produced it and the policy result that let it through. The
    /// alternative — a ranked list with no explanation — is a feed nobody can debug, tune or
    /// defend to the member looking at it.
    /// </summary>
    [Fact]
    public void Every_recommendation_carries_its_explanation()
    {
        var item = Type.GetType(
            "Diaspora.Modules.Recommendations.Domain.Sets.RecommendationItem, Diaspora.Modules.Recommendations");

        item.Should().NotBeNull("the recommendation item is B14's central type");

        var properties = item!.GetProperties().Select(property => property.Name).ToList();

        properties.Should().Contain("Score", "a recommendation without a score cannot be ranked");
        properties.Should().Contain(
            "PrimaryStrategyKey", "a recommendation without its strategy cannot be tuned");
        properties.Should().Contain(
            "Contributions", "the score breakdown is what makes a ranking arguable rather than magic");
        properties.Should().Contain(
            "Explanation", "a recommendation the member cannot be told the reason for is opaque");

        // And the explanation is not an empty shell. B14's promise is a reason code a client can
        // render, not a nullable string somebody meant to fill in later.
        item.GetProperty("Explanation")!.PropertyType
            .GetProperties()
            .Select(property => property.Name)
            .Should().Contain(
                property => property.Contains("Reason", StringComparison.Ordinal),
                "the explanation carries the reason code the member is shown");
    }

    // ── AI readiness ─────────────────────────────────────────────────────

    /// <summary>
    /// <b>The seam a future AI phase lands on, guarded before it exists.</b> B14 shaped
    /// <c>IRankingStrategy</c>, <c>ISimilarityProvider</c> and <c>IRecommendationCandidateSource</c>
    /// so an embedding model or an LLM ranker could replace an implementation without a line of
    /// business code changing. That shape survives exactly as long as nobody takes the shortcut,
    /// and the shortcut is one <c>using</c> wide.
    /// </summary>
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Anthropic")]
    [InlineData("Azure.AI")]
    [InlineData("SemanticKernel")]
    [InlineData("Pinecone")]
    [InlineData("Qdrant")]
    [InlineData("HuggingFace")]
    [InlineData("Ollama")]
    public void No_module_names_an_ai_vendor(string vendor)
    {
        var offenders = SourceTree.ProductionFiles
            .Where(file =>
                file.Path.Contains("Diaspora.Modules.", StringComparison.Ordinal) ||
                file.Path.Contains("Diaspora.Application", StringComparison.Ordinal) ||
                file.Path.Contains("Diaspora.Domain", StringComparison.Ordinal))
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(directive =>
                    directive.Name?.ToString().StartsWith(vendor, StringComparison.Ordinal) == true)
                .Select(file.Locate))
            .ToList();

        offenders.Should().BeEmpty(
            $"'{vendor}' belongs behind a platform abstraction in an adapter assembly, never in a " +
            "vertical:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// The counterpart: the seams an AI implementation would land on still exist. Without this the
    /// rule above is satisfied by a platform that has no extension point at all.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Recommendations.Extensibility.Ranking.IRankingStrategy")]
    [InlineData("Diaspora.Modules.Recommendations.Extensibility.Similarity.ISimilarityProvider")]
    [InlineData("Diaspora.Modules.Recommendations.Extensibility.Candidates.IRecommendationCandidateSource")]
    [InlineData("Diaspora.Modules.Recommendations.Extensibility.Search.ISearchSignalProvider")]
    public void The_seams_an_ai_implementation_would_replace_still_exist(string seam)
    {
        var type = Type.GetType(seam + ", Diaspora.Modules.Recommendations");

        type.Should().NotBeNull("{0} is where AI enters without a module changing", seam);
        type!.IsInterface.Should().BeTrue();
    }

    // ── Communication ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Only the Communication Platform creates notifications.</b> A vertical that sends its own
    /// has taken on channel preferences, quiet hours, digest batching, localisation and delivery
    /// retries — none of which it can see. B13 learns what happened the way every other consumer
    /// does: from a published contract.
    /// </summary>
    [Fact]
    public void No_business_module_names_the_notification_write_path()
    {
        string[] forbidden = ["INotificationPublisher", "INotificationDispatcher", "NotificationRequest"];

        var offenders = SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("Diaspora.Modules.", StringComparison.Ordinal))
            .Where(file => !file.Path.Contains(
                "Diaspora.Modules.Notifications", StringComparison.Ordinal))
            .Where(file => forbidden.Any(name =>
                file.Root.ToString().Contains(name, StringComparison.Ordinal)))
            .Select(file => file.Path)
            .ToList();

        offenders.Should().BeEmpty(
            "publish an integration event and let B13 decide whether, how and to whom:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The kernel's dormant notification port stays dormant.</b> B1 declared
    /// <c>INotificationPublisher</c> before there was a Communication Platform to implement it;
    /// B1.4 recorded it as F5 — a port that reads as available and is not — and B1.4.1 answered by
    /// obsoleting it as an error rather than implementing or deleting it.
    /// <para>
    /// <b>The rule above and this one guard opposite halves of the same shortcut.</b> That one says
    /// no vertical may <em>name</em> the write path. This one says the write path must stay a thing
    /// that does not work, including for the Communication Platform itself — which that rule
    /// exempts, and which is therefore the one module that could quietly implement it. Reviving the
    /// port means deleting an attribute the kernel carries and this assertion with it, which is a
    /// platform decision taken on purpose rather than a convenience reached for on a Friday.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Platform.Services.Abstractions.INotificationPublisher")]
    [InlineData("Diaspora.Platform.Services.Models.NotificationRequest")]
    [InlineData("Diaspora.Platform.Services.Models.NotificationChannel")]
    public void The_dormant_notification_port_stays_dormant(string name)
    {
        // Reached by metadata name rather than by a `typeof`, because naming these types in source
        // is the compile error the phase installed. A test that had to suppress the rule it is
        // asserting would be asserting the suppression.
        var type = Type.GetType($"{name}, Diaspora.Platform");

        type.Should().NotBeNull(
            "{0} is kept, obsoleted, so the shortcut is answered where it is taken rather than " +
            "left as an empty namespace the next author declares into", name);

        var obsolete = type!.GetCustomAttribute<ObsoleteAttribute>();

        obsolete.Should().NotBeNull("{0} must carry the reason it is not a path", name);
        obsolete!.IsError.Should().BeTrue(
            "a warning is a thing a Release build turns into an error and a Debug build scrolls past");
        obsolete.Message.Should().Contain(
            "integration event",
            "the message has to say what to do instead, not only what not to do");

        Platform.SelectMany(Loadable)
            .Where(candidate => candidate is { IsInterface: false, IsAbstract: false })
            .Where(candidate => Array.Exists(candidate.GetInterfaces(), contract => contract == type))
            .Should().BeEmpty(
                "an implementation of {0} would be a kernel-level notification path open to every " +
                "module, which is the one thing B13 exists to prevent", name);
    }

    /// <summary>
    /// Every module's route prefix constants, read from the metadata classes that declare them.
    /// <para>
    /// <b>Every constant whose name ends in <c>RoutePrefix</c>, not the one called exactly that.</b>
    /// Guides owns two URL spaces — <c>/api/v1/guides</c> and B8.1's <c>/api/v1/knowledge</c> — and
    /// reading only the field named <c>RoutePrefix</c> left the second one unchecked for uniqueness
    /// and for versioning, which is the same blind spot F6 recorded about a module that declared no
    /// constant at all.
    /// </para>
    /// </summary>
    private static IReadOnlyList<(string Owner, string Prefix)> RoutePrefixes() =>
        [.. Platform
            .SelectMany(Loadable)
            .Where(type => type.Name.EndsWith("ModuleMetadata", StringComparison.Ordinal))
            .SelectMany(type => type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field =>
                    field.IsLiteral &&
                    field.FieldType == typeof(string) &&
                    field.Name.EndsWith("RoutePrefix", StringComparison.Ordinal))
                .Select(field => (
                    Owner: $"{type.Name}.{field.Name}",
                    Prefix: (string)field.GetRawConstantValue()!)))];

    private static IEnumerable<Type> Loadable(Assembly assembly)
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
