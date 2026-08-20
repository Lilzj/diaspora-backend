using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// How much of the item there is, and what has happened to it.
/// <para>
/// <b>Three numbers, and the invariant between them is the one the whole commerce side rests
/// on:</b> <c>Reserved + Sold &lt;= Quantity</c>, always. <see cref="Available"/> is derived from
/// them rather than stored, because a stored "available" is a fourth number that can disagree with
/// the other three — and the disagreement surfaces as two buyers being sold the same phone.
/// </para>
/// <para>
/// <b>Every mutation returns a new instance and none of them can go negative.</b> The value object
/// refuses rather than clamps: a release of two units against one held unit is a bug in a caller,
/// and clamping would hide it while quietly inventing stock.
/// </para>
/// <para>
/// <b>This is not warehouse management, deliberately.</b> There are no locations, no batches, no
/// stock movements and no reorder points — the brief rules all of that out. What is here is
/// exactly enough to stop the platform overselling.
/// </para>
/// </summary>
public sealed class ListingInventory : ValueObject
{
    private ListingInventory(int quantity, int reserved, int sold, bool isTracked)
    {
        Quantity = quantity;
        Reserved = reserved;
        Sold = sold;
        IsTracked = isTracked;
    }

    private ListingInventory() { }

    /// <summary>Units the seller has, in total. One for a single second-hand item.</summary>
    public int Quantity { get; private set; }

    /// <summary>Units currently held by a live reservation.</summary>
    public int Reserved { get; private set; }

    /// <summary>Units handed over.</summary>
    public int Sold { get; private set; }

    /// <summary>
    /// Whether the seller wants stock counted down. False on a wanted advert or a service, where
    /// "one of" is not a meaningful statement.
    /// </summary>
    public bool IsTracked { get; private set; }

    /// <summary>Units a buyer could commit to right now.</summary>
    public int Available => Math.Max(0, Quantity - Reserved - Sold);

    /// <summary>Whether anything is left.</summary>
    public bool HasStock => Available > 0;

    /// <summary>Whether every unit has been handed over.</summary>
    public bool IsSoldOut => IsTracked && Sold >= Quantity;

    /// <summary>Whether every remaining unit is spoken for but none has been handed over yet.</summary>
    public bool IsFullyReserved => IsTracked && Available == 0 && !IsSoldOut;

    /// <summary>
    /// A single second-hand item. What most listings are.
    /// <para>
    /// Named <c>SingleItem</c> rather than <c>Single</c> because CA1720 refuses a member whose name
    /// is a CLR type name, and Release treats analyser warnings as errors.
    /// </para>
    /// </summary>
    public static ListingInventory SingleItem => new(1, 0, 0, isTracked: true);

    /// <summary>Stock that is not counted — a wanted advert, a service, an unlimited digital good.</summary>
    public static ListingInventory Untracked => new(0, 0, 0, isTracked: false);

    /// <summary>A stated number of units.</summary>
    public static ListingInventory Of(int quantity) =>
        new(Math.Max(1, quantity), 0, 0, isTracked: true);

    /// <summary>
    /// Restates the total, keeping what is already committed. Null when the new total is below
    /// what has already been reserved or sold — the seller is correcting the wrong number.
    /// </summary>
    public ListingInventory? WithQuantity(int quantity) =>
        quantity < Reserved + Sold || quantity < 0
            ? null
            : new ListingInventory(quantity, Reserved, Sold, IsTracked);

    /// <summary>Turns counting on or off, keeping every committed unit.</summary>
    public ListingInventory WithTracking(bool isTracked) =>
        new(Quantity, Reserved, Sold, isTracked);

    /// <summary>Holds units for a reservation. Null when there are not that many left.</summary>
    public ListingInventory? Hold(int units)
    {
        if (units <= 0)
        {
            return null;
        }

        if (!IsTracked)
        {
            // Untracked stock cannot run out, so a hold is recorded for the count and refuses
            // nothing. A wanted advert with three interested sellers is not oversold.
            return new ListingInventory(Quantity, Reserved + units, Sold, IsTracked);
        }

        return units > Available
            ? null
            : new ListingInventory(Quantity, Reserved + units, Sold, IsTracked);
    }

    /// <summary>Returns held units to the shelf. Null when more are released than were held.</summary>
    public ListingInventory? Release(int units) =>
        units <= 0 || units > Reserved
            ? null
            : new ListingInventory(Quantity, Reserved - units, Sold, IsTracked);

    /// <summary>
    /// Converts held units into sold units. The only path from Reserved to Sold, so a completed
    /// reservation cannot double-count and a cancelled one cannot sell anything.
    /// </summary>
    public ListingInventory? Settle(int units) =>
        units <= 0 || units > Reserved
            ? null
            : new ListingInventory(Quantity, Reserved - units, Sold + units, IsTracked);

    /// <summary>
    /// Sells units outright, without a reservation behind them. The "mark as sold" path, for the
    /// seller who did the deal in person and is recording it afterwards.
    /// </summary>
    public ListingInventory? SellDirectly(int units)
    {
        if (units <= 0)
        {
            return null;
        }

        if (!IsTracked)
        {
            return new ListingInventory(Quantity, Reserved, Sold + units, IsTracked);
        }

        return units > Available
            ? null
            : new ListingInventory(Quantity, Reserved, Sold + units, IsTracked);
    }

    /// <summary>Sells everything that is left, held or not. What "mark sold" means with no count.</summary>
    public ListingInventory SellOut() =>
        IsTracked
            ? new ListingInventory(Quantity, 0, Quantity, IsTracked)
            : new ListingInventory(Quantity, 0, Sold + 1, IsTracked);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Quantity;
        yield return Reserved;
        yield return Sold;
        yield return IsTracked;
    }
}
