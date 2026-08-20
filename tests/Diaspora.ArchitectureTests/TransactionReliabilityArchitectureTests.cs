using System.Reflection;
using Diaspora.Domain.Abstractions;
using Diaspora.Persistence;
using Diaspora.Platform.Transactions;
using FluentAssertions;
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
    /// One place creates execution strategies, and it is the unit of work. A second would be a
    /// second opinion on whether a replay is safe.
    /// </summary>
    [Fact]
    public void Only_the_unit_of_work_creates_an_execution_strategy()
    {
        var callers = PersistenceAssembly.GetTypes()
            .Where(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static)
                .Any(method => method.Name.Contains("ExecutionStrategy", StringComparison.Ordinal)))
            .Select(type => type.Name)
            .ToArray();

        callers.Should().BeEmpty(
            "creating a strategy is DiasporaDbContext's own affair, done inline rather than " +
            "through a helper anything else could call");
    }

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
    /// </summary>
    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Sqlite")]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    public void The_transaction_path_is_not_selected_by_provider_name(string providerName)
    {
        var source = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "Diaspora.Persistence", "DiasporaDbContext.cs"));

        var transactionSection = source[source.IndexOf(
            "ExecuteTransactionalAsync<TResult>", StringComparison.Ordinal)..];

        transactionSection.Should().NotContain(
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
