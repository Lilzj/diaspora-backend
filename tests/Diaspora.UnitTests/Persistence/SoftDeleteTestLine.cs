using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// An item in an owned collection, living in its own table.
/// <para>
/// The silent half of D-1. A cascade-deleted owned collection produces valid <c>DELETE</c>
/// statements that succeed, so a soft-deleted aggregate loses its children without any error —
/// and a later restore returns an aggregate that looks intact and is not.
/// </para>
/// </summary>
public sealed class SoftDeleteTestLine : Entity<SoftDeleteTestLineId>
{
    private SoftDeleteTestLine() { }

    private SoftDeleteTestLine(SoftDeleteTestLineId id, string sku, int quantity) : base(id)
    {
        Sku = sku;
        Quantity = quantity;
    }

    public string Sku { get; private set; } = default!;

    public int Quantity { get; private set; }

    internal static SoftDeleteTestLine Create(string sku, int quantity) =>
        new(SoftDeleteTestLineId.New(), sku, quantity);

    internal void Rename(string sku) => Sku = sku;
}
