using System.Reflection;
using Diaspora.Domain.Abstractions;
using Diaspora.Persistence;
using Diaspora.Platform.Transactions;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore.Storage;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundary B1.0.2 depends on: the unit of work owns transactional execution, and nothing else
/// knows it exists.
/// <para>
/// A retry policy is the kind of thing that gets reinvented locally. A module that catches a
/// transient database exception and loops has, without meaning to, opted out of the platform's
/// verification, its replay guard and its telemetry — and it works, right up until the retry lands
/// on a transaction that already committed. These tests are how that stays impossible.
/// </para>
/// </summary>
public sealed class TransactionReliabilityArchitectureTests
{
    private static readonly string[] ModuleAssemblyNames =
    [
        "Diaspora.Modules.Trust",
        "Diaspora.Modules.Location",
        "Diaspora.Modules.Media",
        "Diaspora.Modules.Community",
        "Diaspora.Modules.Providers",
        "Diaspora.Modules.Guides",
        "Diaspora.Modules.Bookmarks"
    ];

    private static IReadOnlyList<Assembly> ModuleAssemblies =>
        ModuleAssemblyNames.Select(name => Assembly.Load(new AssemblyName(name))).ToArray();

    private static Assembly PersistenceAssembly => typeof(DiasporaDbContext).Assembly;

    // ── Modules never touch execution strategies ─────────────────────────

    /// <summary>
    /// <b>The rule.</b> Execution strategies, transactions and retry limits are the platform's to
    /// hold. A module that names one has taken a position on transactional reliability, and it
    /// would be a position taken without the verification, the replay guard or the telemetry that
    /// make the platform's correct.
    /// <para>
    /// Named type by type rather than by banning
    /// <c>Microsoft.EntityFrameworkCore.Storage</c> wholesale, because value converters live under
    /// that namespace too and every module with a database contributor legitimately uses them. A
    /// rule that is too broad gets suppressed, and a suppressed rule protects nothing.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException")]
    public void No_business_module_names_an_execution_strategy_concept(string forbidden)
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn(forbidden)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{assembly.GetName().Name} must leave transactional execution to the unit of " +
                $"work: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    /// <summary>
    /// Nor may a module name a database provider. A retry that is correct on PostgreSQL and wrong
    /// on SQLite is exactly the leak the platform's capability check exists to prevent.
    /// </summary>
    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("Microsoft.Data.SqlClient")]
    public void No_business_module_names_a_database_provider(string provider)
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn(provider)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{assembly.GetName().Name} names {provider}: " +
                string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    // ── The platform owns execution ──────────────────────────────────────

    /// <summary>
    /// One place <em>executes</em> a transaction, and it is the unit of work. A second would be a
    /// second opinion on whether a replay is safe. Two places create an execution strategy, and
    /// both are named below.
    /// <para>
    /// <b>Three halves, because the first one never asserted this and the sentence above used to
    /// say something the platform did not do.</b> The member check forbids a <em>helper</em> — a
    /// shared <c>CreateExecutionStrategy</c> anything in the assembly could call — and it was the
    /// whole rule until 21 November 2026. It passes just as happily with five call sites in five
    /// types as with one, and it scans only the persistence assembly, so it never saw that
    /// <c>TransactionHealthCheck</c> in the API had been creating one all along. Fourth time this
    /// ledger has found a guardrail enforcing one direction of a property and not the one that
    /// mattered.
    /// </para>
    /// <para>
    /// <b>And creating one turned out to be the wrong subject.</b> The health check is correct: it
    /// creates a strategy to read <c>RetriesOnFailure</c> off it, reports whether commit
    /// verification is in play on this deployment, and executes nothing through it. What a second
    /// opinion on transactional reliability actually needs is to <em>open a transaction</em>, so
    /// that is what the third part counts — and there, one file is the whole answer.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_the_unit_of_work_creates_an_execution_strategy()
    {
        var helpers = PersistenceAssembly.GetTypes()
            .Where(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static)
                .Any(method => method.Name.Contains("ExecutionStrategy", StringComparison.Ordinal)))
            .Select(type => type.Name)
            .ToArray();

        helpers.Should().BeEmpty(
            "a strategy is created inline, never through a helper anything else could call");

        // Exact in both directions, so a third entry fails and so does losing either of these.
        string[] mayCreateOne =
        [
            // Reads RetriesOnFailure to report the deployment's capability. Executes nothing.
            "src/Diaspora.Api/Health/TransactionHealthCheck.cs",
            "src/Diaspora.Persistence/Transactions/TransactionalExecutor.cs"
        ];

        var creators = CallSitesOf("CreateExecutionStrategy")
            .Select(location => location[..location.LastIndexOf(':')])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        creators.Should().Equal(
            mayCreateOne,
            "a new one is a position on transactional reliability taken outside the unit of work");

        var openers = CallSitesOf("BeginTransactionAsync");

        openers.Should().NotBeEmpty(
            "a scan that finds no transaction being opened is a broken scan, not a clean platform");

        openers.Should().OnlyContain(
            location => location.StartsWith(
                "src/Diaspora.Persistence/Transactions/TransactionalExecutor.cs:", StringComparison.Ordinal),
            "one place opens a database transaction, and it is the one that writes the receipt, " +
            "verifies the commit and refuses an unsafe replay. Found: {0}",
            string.Join(", ", openers));
    }

    /// <summary>
    /// Every invocation of a method by that name across <c>src/</c>, as clickable locations.
    /// <para>
    /// Roslyn rather than a text search, because the executor's own documentation names
    /// <c>BeginTransactionAsync</c> in prose to explain why it must not be called directly — and a
    /// grep would count that sentence as a call site.
    /// </para>
    /// </summary>
    private static List<string> CallSitesOf(string methodName) =>
        SourceTree.ProductionFiles
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(call => call.Expression is MemberAccessExpressionSyntax member
                               && string.Equals(
                                   member.Name.Identifier.ValueText, methodName, StringComparison.Ordinal))
                .Select(file.Locate))
            .ToList();

    /// <summary>
    /// The transactional contract a handler sees is <see cref="IUnitOfWork"/> and nothing else. No
    /// strategy, no retry count, no verification delegate, no cancellation of a commit.
    /// </summary>
    [Fact]
    public void The_unit_of_work_exposes_no_execution_strategy_concepts()
    {
        var surface = typeof(IUnitOfWork).GetMembers()
            .Select(member => member.Name)
            .ToArray();

        surface.Should().NotContain(name =>
            name.Contains("Strategy", StringComparison.Ordinal) ||
            name.Contains("Retry", StringComparison.Ordinal) ||
            name.Contains("Verify", StringComparison.Ordinal));

        // What it does expose, unchanged since B1.0.1.
        surface.Should().Contain("SaveChangesAsync");
        surface.Should().Contain("ExecuteTransactionalAsync");
        surface.Should().Contain("get_HasActiveTransaction");
    }

    /// <summary>
    /// The failure a caller can observe is a platform type, not EF's. Nothing above the persistence
    /// boundary may reference EF Core, so a <c>RetryLimitExceededException</c> reaching a handler
    /// would be unreferenceable as well as wrong.
    /// </summary>
    [Fact]
    public void The_transient_failure_contract_lives_in_the_domain_abstractions()
    {
        typeof(TransientTransactionFailureException).Assembly
            .Should().BeSameAs(typeof(IUnitOfWork).Assembly);

        typeof(TransientTransactionFailureException).Should().BeDerivedFrom<Exception>();
    }

    // ── Provider independence ────────────────────────────────────────────

    /// <summary>
    /// <b>The assertion that makes "provider-agnostic" a fact rather than a claim.</b> The platform
    /// decides whether to verify commits by asking the strategy whether it retries, never by
    /// testing a provider name. A string comparison against "Npgsql" here would silently do nothing
    /// on SQL Server and on whatever provider is added next.
    /// <para>
    /// <b>It used to read a slice of <c>DiasporaDbContext.cs</c> from an anchor to the end of the
    /// file, and that was load-bearing in a way nothing recorded.</b> The context also held the
    /// SQLite <c>DateTimeOffset</c> convention, whose body necessarily contains the literal
    /// <c>"Sqlite"</c>, so the rule only passed because that method happened to sit <em>above</em>
    /// the anchor. Reordering the file would have failed it for no reason at all. BSOLID-002 moved
    /// the conventions to <c>ModelConventions</c> and transactional execution to its own file, so
    /// the rule now reads a whole file and depends on nothing but what is in it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Sqlite")]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    public void The_transaction_path_is_not_selected_by_provider_name(string providerName)
    {
        var path = Path.Combine(
            RepositoryRoot(), "src", "Diaspora.Persistence", "Transactions", "TransactionalExecutor.cs");

        File.Exists(path).Should().BeTrue(
            "the transactional execution path is what this rule is about; if it moved, this rule " +
            "has to move with it rather than quietly read nothing");

        File.ReadAllText(path).Should().NotContain(
            $"\"{providerName}\"",
            "the choice must key off IExecutionStrategy.RetriesOnFailure, which every provider " +
            "answers correctly, including ones that do not exist yet");
    }

    /// <summary>
    /// The receipt exists to be verifiable across a lost connection, so it must not depend on a
    /// value converter — a raw insert bypasses converters and an EF read applies them, and the two
    /// would disagree about what was stored.
    /// </summary>
    [Fact]
    public void The_receipt_uses_only_primitive_column_types()
    {
        var properties = typeof(TransactionReceipt)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite || property.SetMethod is not null)
            .ToArray();

        properties.Should().OnlyContain(
            property => property.PropertyType == typeof(string) || property.PropertyType == typeof(long),
            "a converted column would round-trip differently depending on whether ADO.NET or EF " +
            "wrote it, and the verifying query would answer 'no' for a commit that succeeded");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Diaspora.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
