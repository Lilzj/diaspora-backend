using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// BARCH-004. One <c>DbContext</c> serves 21 modules, and each module owns a Postgres schema.
/// <para>
/// <b>The startup check that enforces it had no test.</b> <c>ApplyModuleConfigurations</c> compares
/// every entity a contributor adds against the schema that contributor declares and throws if one
/// landed somewhere else — and its own comment says why: "nothing stops the Jobs module from
/// mapping a table into the identity schema, and nothing would notice until an extraction attempt
/// years later found the two tangled together". A guard whose failure path never runs is a guard
/// nobody has seen work.
/// </para>
/// <para>
/// <b>Each host replaces the model cache key.</b> EF caches one model per <c>DbContext</c> CLR
/// type, so a test that builds <c>DiasporaDbContext</c> from its own contributor set would
/// otherwise be handed whichever model ran first — the hazard BARCH-004 records as a real
/// consequence of the single-context design, and the one that broke <c>SoftDeleteCascadeTests</c>
/// when B12.1 added two persistence classes.
/// </para>
/// </summary>
public class ModuleSchemaOwnershipTests
{
    [Fact]
    public void A_module_that_maps_into_its_own_schema_is_accepted()
    {
        var build = () => Model(new WellBehavedContributor());

        build.Should().NotThrow(
            "a contributor that keeps its tables in the schema it declares is the ordinary case, " +
            "and 21 modules depend on it staying ordinary");
    }

    [Fact]
    public void A_module_that_maps_into_another_modules_schema_is_refused_at_startup()
    {
        var build = () => Model(new TrespassingContributor());

        build.Should().Throw<InvalidOperationException>(
            "one shared context means the database will happily accept a module writing into " +
            "another module's schema, and nothing downstream would notice")
            .WithMessage("*trespasser*")
            .WithMessage("*identity*");
    }

    [Fact]
    public void The_refusal_names_the_module_the_schema_and_the_entity()
    {
        var build = () => Model(new TrespassingContributor());

        var message = build.Should().Throw<InvalidOperationException>().Which.Message;

        message.Should().Contain("Trespasser", "the module has to be named or nobody knows whose it is");
        message.Should().Contain("trespasser", "the schema it declared");
        message.Should().Contain("StrayRow", "the entity that landed in the wrong place");
        message.Should().Contain("identity", "the schema it landed in");
    }

    private static IModel Model(params IDatabaseContributor[] contributors)
    {
        var options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .ReplaceService<IModelCacheKeyFactory, SchemaOwnershipModelCacheKeyFactory>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new DiasporaDbContext(
            options, currentUser: null, clock: null, eventDispatcher: null, contributors);

        return context.Model;
    }

    private sealed class WellBehavedContributor : IDatabaseContributor
    {
        public string Module => "WellBehaved";

        public string Schema => "wellbehaved";

        public void Configure(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<OwnRow>(entity =>
            {
                entity.ToTable("own_rows", Schema);
                entity.HasKey(row => row.Id);
            });
        }
    }

    /// <summary>
    /// Declares one schema and maps into another — the Jobs-into-identity shape the context's own
    /// comment names, written as a test rather than left as a sentence.
    /// </summary>
    private sealed class TrespassingContributor : IDatabaseContributor
    {
        public string Module => "Trespasser";

        public string Schema => "trespasser";

        public void Configure(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<StrayRow>(entity =>
            {
                entity.ToTable("stray_rows", "identity");
                entity.HasKey(row => row.Id);
            });
        }
    }

    private sealed class OwnRow
    {
        public Guid Id { get; set; }
    }

    private sealed class StrayRow
    {
        public Guid Id { get; set; }
    }

    private sealed class SchemaOwnershipModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (typeof(SchemaOwnershipModelCacheKeyFactory), Guid.NewGuid(), designTime);
    }
}
