using FluentAssertions;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// The three EF Core rules, each against the exact shape that shipped.
/// </summary>
public sealed class PersistenceConventionAnalyzerTests
{
    // ── DSP3001: Revision ────────────────────────────────────────────────

    [Fact]
    public void A_revision_mapped_as_an_ordinary_column_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class ThingConfiguration : IEntityTypeConfiguration<Thing>
            {
                public void Configure(EntityTypeBuilder<Thing> builder)
                {
                    builder.Property(thing => thing.Revision).HasColumnName("revision");
                }
            }
            """));

        ids.Should().Contain("DSP3001");
    }

    /// <summary>
    /// The call may sit anywhere in the chain. A rule that demanded a position would be a style
    /// rule delivering a correctness rule's message.
    /// </summary>
    [Fact]
    public void A_revision_marked_a_concurrency_token_anywhere_in_the_chain_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class ThingConfiguration : IEntityTypeConfiguration<Thing>
            {
                public void Configure(EntityTypeBuilder<Thing> builder)
                {
                    builder.Property(thing => thing.Revision)
                        .HasColumnName("revision")
                        .IsConcurrencyToken()
                        .HasComment("optimistic concurrency");
                }
            }
            """));

        ids.Should().BeEmpty();
    }

    // ── DSP3002: owned collections ───────────────────────────────────────

    /// <summary>
    /// <b>The defect B11 paid for.</b> Left to convention the collection gets a store-generated
    /// shadow integer inside a composite primary key, and the insert fails the first time a second
    /// row exists — which is why it survives every test that starts from an empty table.
    /// </summary>
    [Fact]
    public void An_owned_collection_without_a_key_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class ThingConfiguration : IEntityTypeConfiguration<Thing>
            {
                public void Configure(EntityTypeBuilder<Thing> builder)
                {
                    builder.OwnsMany(thing => thing.Tags, tag =>
                    {
                        tag.Property(t => t.Name).HasMaxLength(64);
                    });
                }
            }
            """));

        ids.Should().Contain("DSP3002");
    }

    [Fact]
    public void An_owned_collection_that_declares_its_key_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class ThingConfiguration : IEntityTypeConfiguration<Thing>
            {
                public void Configure(EntityTypeBuilder<Thing> builder)
                {
                    builder.OwnsMany(thing => thing.Tags, tag =>
                    {
                        tag.HasKey(t => t.Id);
                        tag.Property(t => t.Id).ValueGeneratedNever();
                        tag.Property(t => t.Name).HasMaxLength(64);
                    });
                }
            }
            """));

        ids.Should().BeEmpty();
    }

    // ── DSP3003: identifiers in a query ──────────────────────────────────

    /// <summary>
    /// <b>The rule B9, B10 and B11 each closed by recommending.</b> B10 shipped this into five
    /// repositories in one phase: it compiles, it passes a suite that reads before it writes, and
    /// it makes page two of every feed a 500 the first time a cursor is used.
    /// </summary>
    [Fact]
    public void Stringifying_an_identifier_inside_a_predicate_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Cursor
            {
                public IQueryable<Thing> After(IThingRepository things, string boundary) =>
                    things.Query.Where(thing => string.Compare(thing.Id.Value.ToString(), boundary) > 0);
            }
            """));

        ids.Should().Contain("DSP3003");
    }

    [Fact]
    public void Comparing_an_identifier_inside_an_ordering_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Cursor
            {
                public IQueryable<Thing> Ordered(IThingRepository things) =>
                    things.Query.OrderBy(thing => thing.Id.ToString());
            }
            """));

        ids.Should().Contain("DSP3003");
    }

    /// <summary>
    /// <b>The two shapes the rule must not touch.</b> Ordering by a slug is the platform's
    /// documented tie-break and translates on every provider; stringifying an identifier inside a
    /// projection is how the response DTO gets built, and happens after the rows have already
    /// arrived.
    /// </summary>
    [Fact]
    public void Ordering_by_a_slug_and_projecting_an_identifier_are_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Cursor
            {
                public IQueryable<Thing> Ordered(IThingRepository things) =>
                    things.Query.OrderBy(thing => thing.Slug);

                public IEnumerable<string> Render(IEnumerable<Thing> things) =>
                    things.Select(thing => thing.Id.Value.ToString());
            }
            """));

        ids.Should().BeEmpty();
    }
}
