using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence;

/// <summary>An owned value object nested inside another owned value object.</summary>
public sealed class SoftDeleteTestCurrency : ValueObject
{
    private SoftDeleteTestCurrency() { }

    private SoftDeleteTestCurrency(string code)
    {
        Code = code;
    }

    public string Code { get; private set; } = default!;

    public static SoftDeleteTestCurrency Create(string code) => new(code);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}
