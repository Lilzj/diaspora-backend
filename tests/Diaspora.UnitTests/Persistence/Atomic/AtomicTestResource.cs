using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>Identifies an <see cref="AtomicTestResource"/>.</summary>
public sealed record AtomicTestResourceId(Guid Value) : StronglyTypedId(Value)
{
    public static AtomicTestResourceId New() => new(Guid.NewGuid());
}

/// <summary>What state a resource is in. Stored as text, so the conditional-transition tests
/// exercise a converted column rather than an integer.</summary>
public enum AtomicTestStatus
{
    Open,
    Committed,
    Closed
}

/// <summary>
/// The counters, in an owned value object on the aggregate's own row.
/// <para>
/// Owned rather than three plain properties, because that is where every counter on this platform
/// actually lives — <c>ListingInventory</c>, <c>HousingAvailability</c> — and a primitive tested
/// against flat columns would not have proved it can reach them.
/// </para>
/// </summary>
public sealed class AtomicTestCapacity : ValueObject
{
    private AtomicTestCapacity(int total, int held, int consumed)
    {
        Total = total;
        Held = held;
        Consumed = consumed;
    }

    private AtomicTestCapacity() { }

    public int Total { get; private set; }

    public int Held { get; private set; }

    public int Consumed { get; private set; }

    public int Available => Math.Max(0, Total - Held - Consumed);

    public static AtomicTestCapacity Of(int total) => new(total, 0, 0);

    public AtomicTestCapacity? Hold(int units) =>
        units <= 0 || units > Available ? null : new AtomicTestCapacity(Total, Held + units, Consumed);

    public AtomicTestCapacity Release(int units) =>
        new(Total, Math.Max(0, Held - Math.Max(0, units)), Consumed);

    public AtomicTestCapacity Consume(int units)
    {
        var moving = Math.Clamp(units, 0, Held);

        return new AtomicTestCapacity(Total, Held - moving, Consumed + moving);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Total;
        yield return Held;
        yield return Consumed;
    }
}

/// <summary>
/// A finite resource: the shape Marketplace stock, Housing units, seats and slots all share, with
/// none of their vocabulary.
/// </summary>
public sealed class AtomicTestResource : AggregateRoot<AtomicTestResourceId>, IAuditableEntity
{
    private AtomicTestResource() { }

    private AtomicTestResource(AtomicTestResourceId id, string name, int total) : base(id)
    {
        Name = name;
        Capacity = AtomicTestCapacity.Of(total);
    }

    public string Name { get; private set; } = default!;

    public AtomicTestCapacity Capacity { get; private set; } = default!;

    public AtomicTestStatus Status { get; private set; } = AtomicTestStatus.Open;

    /// <summary>The optimistic-concurrency token, so the tests can prove the guarded update
    /// deliberately does not trip it.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static AtomicTestResource Create(string name, int total) =>
        new(AtomicTestResourceId.New(), name, total);

    /// <summary>An in-memory hold, of the kind an aggregate makes before the database arbitrates.</summary>
    public bool Hold(int units)
    {
        var held = Capacity.Hold(units);

        if (held is null)
        {
            return false;
        }

        Capacity = held;
        return true;
    }

    public void ReleaseUnits(int units) => Capacity = Capacity.Release(units);

    public void ConsumeUnits(int units) => Capacity = Capacity.Consume(units);

    public void Rename(string name)
    {
        Name = name;
        Revision++;
    }

    public void MoveTo(AtomicTestStatus status) => Status = status;

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
}
