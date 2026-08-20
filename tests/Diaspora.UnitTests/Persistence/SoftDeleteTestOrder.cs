using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// A soft-deletable aggregate that owns every shape EF cascades differently.
/// <para>
/// Deliberately the union of what broke in B5, in one type: an owned reference on a shared table
/// with not-null columns, a second owned reference containing a <em>nested</em> owned value
/// object, and an owned collection in a table of its own. Reproducing D-1 needed all three,
/// because each fails in its own way and a fix for one is not a fix for the others.
/// </para>
/// <para>
/// It also carries a concurrency token, so the tests can prove that repairing the cascade did
/// not quietly disarm optimistic concurrency on the delete path.
/// </para>
/// </summary>
public sealed class SoftDeleteTestOrder
    : AggregateRoot<SoftDeleteTestOrderId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<SoftDeleteTestLine> _lines = [];

    private SoftDeleteTestOrder() { }

    private SoftDeleteTestOrder(
        SoftDeleteTestOrderId id,
        string reference,
        SoftDeleteTestAddress shippingAddress,
        SoftDeleteTestMoney total) : base(id)
    {
        Reference = reference;
        ShippingAddress = shippingAddress;
        Total = total;
        Version = Guid.NewGuid();
    }

    public string Reference { get; private set; } = default!;

    /// <summary>Owned reference, shared table, not-null columns.</summary>
    public SoftDeleteTestAddress ShippingAddress { get; private set; } = default!;

    /// <summary>Owned reference containing a nested owned value object.</summary>
    public SoftDeleteTestMoney Total { get; private set; } = default!;

    /// <summary>Owned collection, its own table.</summary>
    public IReadOnlyList<SoftDeleteTestLine> Lines => _lines.AsReadOnly();

    /// <summary>An optimistic-concurrency token EF adds to the WHERE clause of every update.</summary>
    public Guid Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    public static SoftDeleteTestOrder Create(
        string reference, string city = "Port Louis", string postcode = "11324") =>
        new(
            SoftDeleteTestOrderId.New(),
            reference,
            SoftDeleteTestAddress.Create(city, postcode),
            SoftDeleteTestMoney.Create(42.50m, "MUR"));

    public SoftDeleteTestOrder WithLines(params (string Sku, int Quantity)[] lines)
    {
        foreach (var (sku, quantity) in lines)
        {
            _lines.Add(SoftDeleteTestLine.Create(sku, quantity));
        }

        return this;
    }

    public void MoveTo(string city, string postcode) =>
        ShippingAddress = SoftDeleteTestAddress.Create(city, postcode);

    public void Reprice(decimal amount, string currencyCode) =>
        Total = SoftDeleteTestMoney.Create(amount, currencyCode);

    public void RenameFirstLine(string sku) => _lines[0].Rename(sku);

    public void Rename(string reference) => Reference = reference;

    /// <summary>
    /// Moves the concurrency token on, the way a real aggregate would on any state change.
    /// Lets a test create a genuine stale-token conflict without reaching for raw SQL.
    /// </summary>
    public void Touch() => Version = Guid.NewGuid();

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
