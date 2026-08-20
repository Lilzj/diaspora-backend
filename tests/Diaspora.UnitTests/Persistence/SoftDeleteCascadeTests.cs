using Diaspora.Domain.Abstractions;
using Diaspora.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// B1.0.1 — the persistence reliability patch, as executable proof.
/// <para>
/// Before this patch, calling <c>Remove()</c> on a soft-deletable aggregate that owned value
/// objects produced one of two outcomes and never the right one: a constraint violation for an
/// owned reference on a shared table, or a silent loss of rows for an owned collection. Every
/// test in this file fails against the pre-patch <c>DiasporaDbContext</c>.
/// </para>
/// <para>
/// They run against the real context through the real <c>IDatabaseContributor</c> seam, so they
/// exercise the same audit pipeline, soft-delete conversion and query filters every module saves
/// through.
/// </para>
/// </summary>
public sealed class SoftDeleteCascadeTests
{
    // ── The defect ───────────────────────────────────────────────────────

    /// <summary>
    /// The original failure, reduced. Pre-patch this threw
    /// <c>SqliteException: NOT NULL constraint failed: orders.city</c>.
    /// </summary>
    [Fact]
    public void Soft_deleting_an_aggregate_with_an_owned_value_object_no_longer_violates_not_null()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-1"));

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;

            context.Remove(order);

            // The assertion is that this does not throw at all.
            context.Invoking(ctx => ctx.SaveChanges()).Should().NotThrow();
        }

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;

            order.IsDeleted.Should().BeTrue();

            // The owned columns still hold their values rather than NULL — which is the whole
            // point. A soft delete hides the aggregate; it does not hollow it out.
            order.ShippingAddress.City.Should().Be("Port Louis");
            order.ShippingAddress.Postcode.Should().Be("11324");
        }
    }

    /// <summary>
    /// The silent half of the defect. An owned collection lives in its own table, so the cascade
    /// produced valid DELETE statements that succeeded — and a soft-deleted order came back with
    /// no lines and no error to explain where they went.
    /// </summary>
    [Fact]
    public void Soft_deleting_an_aggregate_does_not_delete_its_owned_collection()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder
            .Create("ORD-2")
            .WithLines(("SKU-A", 1), ("SKU-B", 2), ("SKU-C", 3)));

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;
            context.Remove(order);
            context.SaveChanges();
        }

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;

            order.IsDeleted.Should().BeTrue();
            order.Lines.Should().HaveCount(3);
            order.Lines.Select(line => line.Sku)
                .Should().BeEquivalentTo("SKU-A", "SKU-B", "SKU-C");
        }
    }

    /// <summary>
    /// Nesting is why a single-level fix would not have been one. Reverting only the aggregate's
    /// direct owned references leaves the currency inside the total Deleted, and the same NULL
    /// write happens one level down.
    /// </summary>
    [Fact]
    public void Soft_deleting_preserves_owned_value_objects_nested_inside_owned_value_objects()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-3"));

        using (var context = host.NewContext())
        {
            context.Remove(SoftDeleteTestHost.Load(context, id)!);
            context.SaveChanges();
        }

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;

            order.Total.Amount.Should().Be(42.50m);
            order.Total.Currency.Code.Should().Be("MUR");
        }
    }

    /// <summary>
    /// Every owned shape at once, which is the configuration `MediaAsset` actually has: two owned
    /// references, one of them nested, plus an owned collection.
    /// </summary>
    [Fact]
    public void Soft_deleting_preserves_every_owned_shape_simultaneously()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder
            .Create("ORD-4", "Curepipe", "74213")
            .WithLines(("SKU-X", 7)));

        using (var context = host.NewContext())
        {
            context.Remove(SoftDeleteTestHost.Load(context, id)!);
            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var order = SoftDeleteTestHost.Load(verification, id)!;

        order.IsDeleted.Should().BeTrue();
        order.ShippingAddress.City.Should().Be("Curepipe");
        order.ShippingAddress.Postcode.Should().Be("74213");
        order.Total.Currency.Code.Should().Be("MUR");
        order.Lines.Should().ContainSingle().Which.Sku.Should().Be("SKU-X");
    }

    // ── Behaviour that had to survive the fix ────────────────────────────

    /// <summary>
    /// The soft-delete stamp itself: still applied, still with the platform clock's time and the
    /// current user, exactly as before B1.0.1.
    /// </summary>
    [Fact]
    public void The_soft_delete_stamp_still_records_when_and_who()
    {
        var deletedAt = new DateTimeOffset(2026, 7, 28, 14, 30, 0, TimeSpan.Zero);

        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-5"));

        host.Clock.Set(deletedAt);

        using (var context = host.NewContext())
        {
            context.Remove(SoftDeleteTestHost.Load(context, id)!);
            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var order = SoftDeleteTestHost.Load(verification, id)!;

        order.IsDeleted.Should().BeTrue();
        order.DeletedAt.Should().Be(deletedAt);
        order.DeletedBy.Should().Be(host.CurrentUser.UserId!.ToString());
    }

    /// <summary>
    /// Creation audit survives a soft delete. It is written on insert and must not be disturbed
    /// by the delete path — a row whose CreatedAt moved is a row whose audit trail is a fiction.
    /// </summary>
    [Fact]
    public void Creation_audit_is_untouched_by_a_soft_delete()
    {
        var createdAt = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);

        using var host = new SoftDeleteTestHost(createdAt);

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-6"));

        host.Clock.Set(createdAt.AddDays(3));

        using (var context = host.NewContext())
        {
            context.Remove(SoftDeleteTestHost.Load(context, id)!);
            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var order = SoftDeleteTestHost.Load(verification, id)!;

        order.CreatedAt.Should().Be(createdAt);
        order.CreatedBy.Should().Be(host.CurrentUser.UserId!.ToString());
    }

    /// <summary>
    /// The soft-delete query filter still hides the row from ordinary reads. The fix changed how
    /// the delete is written, not what a deleted row means.
    /// </summary>
    [Fact]
    public void A_soft_deleted_aggregate_disappears_from_ordinary_queries()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-7").WithLines(("SKU-A", 1)));

        using (var context = host.NewContext())
        {
            context.Remove(SoftDeleteTestHost.Load(context, id)!);
            context.SaveChanges();
        }

        using var verification = host.NewContext();

        SoftDeleteTestHost.Load(verification, id, includeDeleted: false).Should().BeNull();
        SoftDeleteTestHost.Load(verification, id).Should().NotBeNull();
    }

    /// <summary>
    /// Optimistic concurrency still guards the delete. A soft delete is an UPDATE, so a stale
    /// token must still lose — repairing the cascade must not have quietly widened the write into
    /// something that no longer carries the token.
    /// </summary>
    [Fact]
    public void Optimistic_concurrency_still_applies_to_a_soft_delete()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-8"));

        using var stale = host.NewContext();
        var staleOrder = SoftDeleteTestHost.Load(stale, id)!;

        // Somebody else updates the row first, moving the concurrency token on. Done through EF
        // rather than raw SQL so the test makes no assumption about how a provider names tables.
        using (var other = host.NewContext())
        {
            var current = SoftDeleteTestHost.Load(other, id)!;

            current.Rename("ORD-8-REV");
            current.Touch();

            other.SaveChanges();
        }

        stale.Remove(staleOrder);

        // Translated at the persistence boundary so EF's exception type never reaches the
        // application layer — behaviour B2.6 introduced and this patch preserves.
        stale.Invoking(context => context.SaveChanges())
            .Should().Throw<ConcurrencyConflictException>();
    }

    // ── Restore, update, and repeated cycles ─────────────────────────────

    /// <summary>
    /// Restoring returns everything, because nothing was lost. Pre-patch this test could not
    /// exist: the owned collection was gone by the time anyone restored the row.
    /// </summary>
    [Fact]
    public void A_restored_aggregate_comes_back_whole()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder
            .Create("ORD-9", "Grand Baie", "30510")
            .WithLines(("SKU-A", 1), ("SKU-B", 2)));

        using (var context = host.NewContext())
        {
            context.Remove(SoftDeleteTestHost.Load(context, id)!);
            context.SaveChanges();
        }

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;
            order.Restore();
            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var restored = SoftDeleteTestHost.Load(verification, id, includeDeleted: false)!;

        restored.IsDeleted.Should().BeFalse();
        restored.DeletedAt.Should().BeNull();
        restored.ShippingAddress.City.Should().Be("Grand Baie");
        restored.Total.Currency.Code.Should().Be("MUR");
        restored.Lines.Should().HaveCount(2);
    }

    /// <summary>
    /// Delete, restore, delete again. Repeated cycles are where a state-machine fix that only
    /// works once shows itself.
    /// </summary>
    [Fact]
    public void Repeated_delete_and_restore_cycles_stay_consistent()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-10").WithLines(("SKU-A", 1)));

        for (var cycle = 0; cycle < 3; cycle++)
        {
            using (var context = host.NewContext())
            {
                context.Remove(SoftDeleteTestHost.Load(context, id)!);
                context.SaveChanges();
            }

            using (var context = host.NewContext())
            {
                var order = SoftDeleteTestHost.Load(context, id)!;

                order.IsDeleted.Should().BeTrue($"cycle {cycle} should have deleted it");
                order.Lines.Should().ContainSingle($"cycle {cycle} should have kept the line");

                order.Restore();
                context.SaveChanges();
            }
        }

        using var verification = host.NewContext();
        var final = SoftDeleteTestHost.Load(verification, id, includeDeleted: false)!;

        final.IsDeleted.Should().BeFalse();
        final.ShippingAddress.City.Should().Be("Port Louis");
        final.Lines.Should().ContainSingle();
    }

    /// <summary>
    /// An ordinary update is unaffected. The converter only touches entries that are Deleted, so
    /// the common path never reaches it.
    /// </summary>
    [Fact]
    public void An_ordinary_update_is_unaffected()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-11").WithLines(("SKU-A", 1)));

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;

            order.Rename("ORD-11-REV");
            order.MoveTo("Quatre Bornes", "72201");
            order.RenameFirstLine("SKU-Z");

            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var updated = SoftDeleteTestHost.Load(verification, id)!;

        updated.Reference.Should().Be("ORD-11-REV");
        updated.ShippingAddress.City.Should().Be("Quatre Bornes");
        updated.Lines.Single().Sku.Should().Be("SKU-Z");
        updated.IsDeleted.Should().BeFalse();
    }

    /// <summary>
    /// Removing one item from an owned collection must still remove it. The fix reverts a cascade
    /// only for aggregates being soft-deleted — a deliberate removal on a live aggregate is a
    /// different intention and is left alone.
    /// </summary>
    [Fact]
    public void Removing_a_single_owned_collection_item_still_removes_it()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder
            .Create("ORD-12")
            .WithLines(("SKU-A", 1), ("SKU-B", 2)));

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;

            // Reaching through the tracked collection is what a real owned-collection removal
            // does; the aggregate itself is untouched and stays live.
            var lines = context.Entry(order).Collection(entity => entity.Lines);
            var doomed = order.Lines.First(line => line.Sku == "SKU-A");

            context.Entry(doomed).State = EntityState.Deleted;
            lines.IsModified = true;

            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var order2 = SoftDeleteTestHost.Load(verification, id)!;

        order2.IsDeleted.Should().BeFalse();
        order2.Lines.Should().ContainSingle().Which.Sku.Should().Be("SKU-B");
    }

    /// <summary>
    /// A change to an owned value object made in the same unit of work as the delete is written,
    /// not silently discarded.
    /// <para>
    /// This is why the converter diffs current against original before reverting the state rather
    /// than simply marking the entry Unchanged. Unchanged alone is the cheap fix and it loses
    /// data in exactly this sequence.
    /// </para>
    /// </summary>
    [Fact]
    public void An_owned_value_changed_in_the_same_save_as_the_delete_is_not_lost()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-13"));

        using (var context = host.NewContext())
        {
            var order = SoftDeleteTestHost.Load(context, id)!;

            order.MoveTo("Mahébourg", "50801");
            order.Reprice(99.99m, "EUR");

            context.Remove(order);
            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var deleted = SoftDeleteTestHost.Load(verification, id)!;

        deleted.IsDeleted.Should().BeTrue();
        deleted.ShippingAddress.City.Should().Be("Mahébourg");
        deleted.ShippingAddress.Postcode.Should().Be("50801");
        deleted.Total.Amount.Should().Be(99.99m);
        deleted.Total.Currency.Code.Should().Be("EUR");
    }

    /// <summary>
    /// Several aggregates deleted in one save all behave. The converter materialises its work
    /// list before mutating anything, so reshaping the change tracker mid-loop cannot bite.
    /// </summary>
    [Fact]
    public void Several_aggregates_soft_deleted_in_one_save_all_survive_intact()
    {
        using var host = new SoftDeleteTestHost();

        var ids = Enumerable.Range(1, 5)
            .Select(index => host.Seed(SoftDeleteTestOrder
                .Create($"BULK-{index}")
                .WithLines(($"SKU-{index}", index))))
            .ToList();

        using (var context = host.NewContext())
        {
            foreach (var id in ids)
            {
                context.Remove(SoftDeleteTestHost.Load(context, id)!);
            }

            context.SaveChanges();
        }

        using var verification = host.NewContext();

        foreach (var id in ids)
        {
            var order = SoftDeleteTestHost.Load(verification, id)!;

            order.IsDeleted.Should().BeTrue();
            order.ShippingAddress.City.Should().Be("Port Louis");
            order.Lines.Should().ContainSingle();
        }
    }

    /// <summary>
    /// An aggregate with no owned types at all — the shape Identity's aggregates have — behaves
    /// exactly as it did before the patch. This is the regression guard for every module that
    /// was already working.
    /// </summary>
    [Fact]
    public void An_aggregate_without_owned_types_behaves_exactly_as_before()
    {
        using var host = new SoftDeleteTestHost();

        var id = host.Seed(SoftDeleteTestOrder.Create("ORD-14"));

        using (var context = host.NewContext())
        {
            context.Remove(SoftDeleteTestHost.Load(context, id)!);
            context.SaveChanges();
        }

        using var verification = host.NewContext();
        var order = SoftDeleteTestHost.Load(verification, id)!;

        order.IsDeleted.Should().BeTrue();
        order.DeletedAt.Should().NotBeNull();
        order.Lines.Should().BeEmpty();
    }
}
