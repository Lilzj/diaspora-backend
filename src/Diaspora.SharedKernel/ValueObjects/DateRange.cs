using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class DateRange : ValueObject
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }
    public TimeSpan Duration => End - Start;

    private DateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    public static DateRange Create(DateTimeOffset start, DateTimeOffset end)
    {
        if (start > end)
            throw new ArgumentException("Start date must be before or equal to end date.", nameof(start));
        return new DateRange(start, end);
    }

    public bool Contains(DateTimeOffset point) => point >= Start && point <= End;
    public bool Overlaps(DateRange other) => Start < other.End && End > other.Start;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
