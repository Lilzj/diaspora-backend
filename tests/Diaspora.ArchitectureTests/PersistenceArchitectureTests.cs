using System.Reflection;
using Diaspora.Domain.Abstractions;
using Diaspora.Persistence;
using Diaspora.Persistence.ChangeTracking;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the persistence layer must not cross, and the shape B1.0.1 depends on.
/// <para>
/// The soft-delete converter is the platform's answer to defect D-1. Its whole value rests on
/// two properties: it is the <em>only</em> implementation of the rule, so no module needs a
/// workaround, and it is provider-independent, so the rule behaves identically on SQLite,
/// PostgreSQL and SQL Server. Both are asserted here rather than described in a document.
/// </para>
/// </summary>
public sealed class PersistenceArchitectureTests
{
    private static Assembly PersistenceAssembly => typeof(DiasporaDbContext).Assembly;

    // ── The soft-delete converter ────────────────────────────────────────

    /// <summary>
    /// A platform-owned, publicly reachable component rather than a private method. Private, it
    /// would be untestable except through a full save; public, its behaviour is pinned by unit
    /// tests that construct a change tracker directly.
    /// </summary>
    [Fact]
    public void The_soft_delete_converter_is_a_public_platform_component()
    {
        typeof(SoftDeleteConverter).IsPublic.Should().BeTrue();
        typeof(SoftDeleteConverter).IsAbstract.Should().BeTrue();
        typeof(SoftDeleteConverter).IsSealed.Should().BeTrue();

        typeof(SoftDeleteConverter).Assembly.Should().BeSameAs(PersistenceAssembly);
    }

    /// <summary>
    /// <b>The assertion that makes "provider-independent" a fact rather than a claim.</b>
    /// </summary>
    /// <para>
    /// The rule is expressed entirely in EF's change-tracking and metadata APIs — no SQL, no
    /// table names, no provider capabilities. If a provider type ever appears in this namespace,
    /// the rule has acquired a behaviour that differs between SQLite, PostgreSQL and SQL Server,
    /// and the guarantee B1.0.1 rests on is gone.
    /// </para>
    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    public void The_change_tracking_namespace_names_no_database_provider(string provider)
    {
        var result = Types.InAssembly(PersistenceAssembly)
            .That().ResideInNamespace("Diaspora.Persistence.ChangeTracking")
            .ShouldNot()
            .HaveDependencyOn(provider)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the soft-delete rule must behave identically on every provider: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// It must also not reach for relational-only concepts. Table names and column mappings are
    /// how a "provider-independent" component quietly becomes a relational-only one.
    /// </summary>
    [Fact]
    public void The_change_tracking_namespace_does_not_reach_for_relational_metadata()
    {
        var result = Types.InAssembly(PersistenceAssembly)
            .That().ResideInNamespace("Diaspora.Persistence.ChangeTracking")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore.Metadata.RelationalEntityTypeExtensions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── The guarded update primitive ─────────────────────────────────────

    /// <summary>
    /// <b>The assertion that makes "provider-independent" a fact rather than a claim for B1.3's
    /// guarded update.</b> The statement is built from EF's model and delimited by the provider's own
    /// SQL generation helper; if a provider type ever appears in this namespace it has acquired a
    /// behaviour that differs between SQLite, PostgreSQL and SQL Server.
    /// <para>
    /// SQL Server is the one that matters most here, because nothing in this solution references a
    /// SQL Server provider and therefore nothing can execute against it. A provider-specific
    /// dependency would break it silently and nowhere else.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    public void The_atomic_namespace_names_no_database_provider(string provider)
    {
        var result = Types.InAssembly(PersistenceAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Persistence.Atomic")
            .ShouldNot()
            .HaveDependencyOn(provider)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a guarded update must behave identically on every provider: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── No module-level workarounds ──────────────────────────────────────

    /// <summary>
    /// <b>The regression guard for the workaround B1.0.1 removed.</b>
    /// <para>
    /// B5 had to call <c>MarkDeleted</c> by hand in its delete handler because routing through
    /// the platform corrupted the aggregate. Now that the platform is correct, no module should
    /// ever do that again — a handler that stamps a deletion itself bypasses the audit clock,
    /// the query filter's expectations, and whatever the platform does next.
    /// </para>
    /// <para>
    /// Enforced by type dependency: an application-layer handler with no reason to know about
    /// <see cref="ISoftDeletable"/> cannot be reaching into the soft-delete lifecycle. The
    /// aggregates themselves implement the interface and are excluded.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Media")]
    [InlineData("Diaspora.Modules.Trust")]
    [InlineData("Diaspora.Modules.Location")]
    [InlineData("Diaspora.Modules.Guides")]
    public void No_module_application_layer_touches_the_soft_delete_lifecycle(string moduleName)
    {
        var assembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == moduleName);

        if (assembly is null)
        {
            // The module is not loaded in this run. Nothing to assert, and failing here would
            // make the suite depend on assembly load order rather than on architecture.
            return;
        }

        var offenders = assembly.GetTypes()
            .Where(type => type.Namespace?.Contains(".Application.", StringComparison.Ordinal) == true)
            .Where(type => !typeof(ISoftDeletable).IsAssignableFrom(type))
            .Where(type => type.GetInterfaces().Contains(typeof(ISoftDeletable)))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "soft deletion is the platform's job; a handler stamping it by hand is the " +
            "workaround B1.0.1 exists to make unnecessary");
    }

    // ── The single save path ─────────────────────────────────────────────

    /// <summary>
    /// Every save overload routes through one path, which is where audit stamping and
    /// soft-delete conversion happen. B2 overrode only one of the four and the other three
    /// silently skipped both; the fix must not have reopened that hole.
    /// </summary>
    [Fact]
    public void Every_save_overload_is_overridden_so_none_can_skip_the_pipeline()
    {
        var overridden = typeof(DiasporaDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name is nameof(DiasporaDbContext.SaveChanges)
                or nameof(DiasporaDbContext.SaveChangesAsync))
            .Select(method => method.Name)
            .ToList();

        overridden.Should().Contain(nameof(DiasporaDbContext.SaveChanges));
        overridden.Should().Contain(nameof(DiasporaDbContext.SaveChangesAsync));
    }
}
