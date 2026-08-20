using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class Money : ValueObject, IComparable<Money>
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }

    private Money(decimal amount, string currencyCode)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
    }

    public static Money Create(decimal amount, string currencyCode)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        Guard.Against.NullOrWhiteSpace(currencyCode, nameof(currencyCode));
        if (currencyCode.Length != 3)
            throw new ArgumentException("Currency code must be 3 characters (ISO 4217).", nameof(currencyCode));
        return new Money(Math.Round(amount, 2), currencyCode.ToUpperInvariant());
    }

    public static Money Zero(string currencyCode) => Create(0, currencyCode);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return Create(Amount + other.Amount, CurrencyCode);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return Create(Amount - other.Amount, CurrencyCode);
    }

    public Money Multiply(decimal factor) =>
        Create(Amount * factor, CurrencyCode);

    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (CurrencyCode != other.CurrencyCode)
            throw new InvalidOperationException($"Cannot operate on different currencies: {CurrencyCode} and {other.CurrencyCode}.");
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static Money operator *(Money left, decimal right) => left.Multiply(right);

    // Equality is structural and comes from ValueObject; these overloads exist so a
    // statically typed Money comparison binds here rather than to the base operator,
    // and so the type satisfies the full IComparable contract.
    public bool Equals(Money? other) => base.Equals(other);

    public override bool Equals(object? obj) => base.Equals(obj);

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(Money? left, Money? right) => Equals(left, right);

    public static bool operator !=(Money? left, Money? right) => !Equals(left, right);

    // Ordering operators to match IComparable. Like CompareTo, these throw when the
    // currencies differ rather than answering a question that has no answer.
    public static bool operator <(Money? left, Money? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(Money? left, Money? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(Money? left, Money? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(Money? left, Money? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return CurrencyCode;
    }

    public override string ToString() => $"{Amount:F2} {CurrencyCode}";
}
