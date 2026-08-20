using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class Timestamp : ValueObject
{
    public DateTimeOffset Value { get; }

    private Timestamp(DateTimeOffset value)
    {
        Value = value.ToUniversalTime();
    }

    public static Timestamp Now() => new(DateTimeOffset.UtcNow);
    public static Timestamp From(DateTimeOffset value) => new(value);

    public static implicit operator DateTimeOffset(Timestamp timestamp) => timestamp.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString("O");
}
