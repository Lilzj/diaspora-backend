using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// An owned value object that itself owns another — the nested case.
/// <para>
/// Nesting is what makes a single-level fix insufficient: reverting only the aggregate's direct
/// owned references leaves <see cref="Currency"/> Deleted one level down, and the same NULL
/// write happens for its column instead.
/// </para>
/// </summary>
public sealed class SoftDeleteTestMoney : ValueObject
{
    private SoftDeleteTestMoney() { }

    private SoftDeleteTestMoney(decimal amount, SoftDeleteTestCurrency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; private set; }

    /// <summary>The nested owned value object.</summary>
    public SoftDeleteTestCurrency Currency { get; private set; } = default!;

    public static SoftDeleteTestMoney Create(decimal amount, string currencyCode) =>
        new(amount, SoftDeleteTestCurrency.Create(currencyCode));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
