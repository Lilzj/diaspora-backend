using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The EF conventions the platform depends on, asserted against the real model rather than
/// described in a document.
/// <para>
/// <b>Every rule here is one B11 paid for, and every one of them was already written down.</b> The
/// owned-collection key was in the persistence guide; the concurrency token was in three modules'
/// architecture notes; the backing-field convention is how every aggregate on the platform exposes
/// a collection. Each was enforced by a human remembering, and each was forgotten — twice, in the
/// case of the request-binding rule that has its own test next door. Reading the model is what
/// turns them into build failures.
/// </para>
/// <para>
/// Model-based rather than reflection-based on purpose. Reflection can see that a property called
/// <c>Revision</c> exists; only the model can see whether anybody configured it. And only the model
/// knows what EF resolved, which is the difference between a rule about how code looks and a rule
/// about what will happen at run time.
/// </para>
/// </summary>
public sealed class PersistenceModelArchitectureTests
{
    /// <summary>
    /// The suite is worthless if the model is empty or partial, and a model built from a scanner
    /// can go partial silently — a renamed assembly, a contributor that stopped being public.
    /// </summary>
    [Fact]
    public void The_model_spans_every_module()
    {
        PlatformModel.Contributors.Select(contributor => contributor.Module)
            .Should().Contain(["Marketplace", "Housing", "Jobs", "Guides", "Community"]);

        PlatformModel.Value.GetEntityTypes().Should().HaveCountGreaterThan(100);
    }

    // ── Rule: owned collections declare an explicit key ──────────────────

    /// <summary>
    /// <b>B11 defect A, as a build failure.</b> Left to convention, EF gives an owned collection a
    /// shadow integer key marked store-generated, inside a composite primary key. PostgreSQL
    /// tolerates it. SQLite — which every integration test runs on — refuses the insert with
    /// <c>NOT NULL constraint failed</c>, and only once a row actually exists, so a suite that
    /// reads before it writes stays green. Four collections in B11 carried it.
    /// <para>
    /// <b>What exactly goes wrong, and therefore what this asserts.</b> The convention key is a
    /// store-generated shadow integer <em>inside a composite</em> primary key alongside the owner's
    /// foreign key. SQLite only auto-generates an integer key when it is the whole primary key — a
    /// rowid alias — so in a composite it generates nothing and the not-null constraint fails. The
    /// rule is therefore not "the key must not be shadow": B8's <c>GuideAuthorBadge</c> declares a
    /// shadow surrogate deliberately, because its natural identity has a nullable half, and it
    /// works precisely because it declares it <em>alone</em>. The rule is that a store-generated
    /// key stands by itself.
    /// </para>
    /// <para>
    /// Configuration source would be the more direct question — did a human call <c>HasKey</c> —
    /// but a finalised model no longer carries it, so <see cref="PersistenceRuleArchitectureTests"/>
    /// asks that of the source instead. The two rules catch the same defect from both ends.
    /// </para>
    /// </summary>
    [Fact]
    public void No_owned_collection_hides_a_store_generated_key_inside_a_composite()
    {
        var offenders = new List<string>();

        foreach (var entityType in PlatformModel.Value.GetEntityTypes())
        {
            var ownership = entityType.FindOwnership();

            if (ownership is null || ownership.IsUnique)
            {
                // Not an owned collection: either not owned at all, or an owned reference sharing
                // the owner's row, which has no key of its own to declare.
                continue;
            }

            var key = entityType.FindPrimaryKey();

            if (key is null)
            {
                offenders.Add($"{entityType.DisplayName()} (no primary key)");
                continue;
            }

            var generated = key.Properties
                .Where(property => property.ValueGenerated != ValueGenerated.Never)
                .ToList();

            if (key.Properties.Count > 1 && generated.Count > 0)
            {
                offenders.Add(
                    $"{entityType.DisplayName()}" +
                    $"[{string.Join(" + ", key.Properties.Select(property => property.Name))}] " +
                    $"— {string.Join(", ", generated.Select(property => property.Name))}");
            }
        }

        offenders.Should().BeEmpty(
            "a store-generated property inside a composite key is filled in by nobody: SQLite " +
            "generates an integer key only when it is the whole primary key, and a caller-supplied " +
            "identifier EF believes it generates makes EF emit an UPDATE where an INSERT was " +
            "meant. Declare ValueGeneratedNever(): " + string.Join("; ", offenders));
    }

    // ── Rule: Revision implies a concurrency token ───────────────────────

    /// <summary>
    /// <b>B11 defect D, as a build failure.</b> Three aggregates carried a <c>Revision</c>, bumped
    /// it on every mutation and checked it in <c>EnsureRevision</c> — and none of them declared it
    /// <c>IsConcurrencyToken()</c>. The aggregate check catches an editor that <em>sends back</em> a
    /// revision; nothing caught the one that does not, and two saves from one landlord's two
    /// devices both committed with the later silently winning.
    /// </summary>
    [Fact]
    public void Every_revision_property_is_a_concurrency_token()
    {
        var offenders = PlatformModel.Value.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                .Where(property =>
                    property.Name == "Revision"
                    && property.ClrType == typeof(int)
                    && !property.IsConcurrencyToken)
                .Select(property => $"{entity.DisplayName()}.{property.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "an aggregate that carries a Revision means to detect a lost update, and only " +
            "IsConcurrencyToken() puts it in the WHERE clause: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The rule above can only see mapped properties, so a <c>Revision</c> on an aggregate nobody
    /// configured would pass it by being invisible. This catches that.
    /// </summary>
    [Fact]
    public void Every_mapped_aggregate_with_a_revision_has_it_mapped()
    {
        var offenders = PlatformModel.Value.GetEntityTypes()
            .Where(entity => entity.ClrType
                .GetProperty("Revision")?.PropertyType == typeof(int))
            .Where(entity => entity.FindProperty("Revision") is null)
            .Select(entity => entity.DisplayName())
            .ToList();

        offenders.Should().BeEmpty(
            "a Revision the model does not know about cannot be a concurrency token, and reads " +
            "as one to anybody looking at the aggregate: " + string.Join(", ", offenders));
    }

    // ── Rule: read-only collections have their backing field ─────────────

    /// <summary>
    /// <b>B11 defect B, as a build failure.</b> Every aggregate on this platform exposes its
    /// collections as <c>IReadOnlyList&lt;T&gt;</c> computed from a private <c>List&lt;T&gt;</c>.
    /// EF cannot write through the getter, so it finds the field — by naming convention and by
    /// nothing else. <c>_media</c> behind <c>MediaReferences</c> compiled, drafted, published and
    /// saved perfectly well, and threw <c>Collection is read-only</c> the first time a listing with
    /// a photograph was read back.
    /// <para>
    /// Asked of the model rather than of reflection, which is what makes it exact. B11 suggested a
    /// reflection loop comparing property names to camel-cased field names; that would flag a
    /// property EF never maps and miss a field EF failed to resolve for some other reason. The
    /// question that matters is not "does a field with the right name exist" but "did EF find one",
    /// and <see cref="IPropertyBase.FieldInfo"/> is EF answering it.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_read_only_collection_navigation_resolves_a_backing_field()
    {
        var offenders = new List<string>();

        foreach (var entityType in PlatformModel.Value.GetEntityTypes())
        {
            foreach (var navigation in entityType.GetNavigations())
            {
                if (!navigation.IsCollection || navigation.FieldInfo is not null)
                {
                    continue;
                }

                // No field, so EF has to go through the property. That only works if it can write
                // to the collection the getter returns — which a read-only projection is not.
                if (navigation.PropertyInfo?.SetMethod is null)
                {
                    offenders.Add(
                        $"{entityType.DisplayName()}.{navigation.Name} " +
                        "(expected a field named _" + Camel(navigation.Name) + ")");
                }
            }
        }

        offenders.Should().BeEmpty(
            "EF writes a read-only collection through its backing field and finds that field by " +
            "name. A mismatch throws 'Collection is read-only' on read-back and nowhere else: " +
            string.Join(", ", offenders));
    }

    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
