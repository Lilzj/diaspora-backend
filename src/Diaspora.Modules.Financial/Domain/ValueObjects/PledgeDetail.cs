using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// What a non-monetary contribution actually is.
/// <para>
/// Three boxes of school books. Six Saturdays at a food bank. Two hours a month of legal advice.
/// <b>None of these is a number of pence, and forcing them through <see cref="Money"/> would have
/// been the easy modelling mistake</b> — a contribution of zero, with the real content in a
/// description nobody can count.
/// </para>
/// <para>
/// A quantity and a unit is enough to total a drive ("47 coats") without pretending the platform
/// knows what a coat is worth.
/// </para>
/// </summary>
public sealed class PledgeDetail : ValueObject
{
    private PledgeDetail(decimal quantity, string unit, string description)
    {
        Quantity = quantity;
        Unit = unit;
        Description = description;
    }

    private PledgeDetail() { }

    public decimal Quantity { get; private set; }

    /// <summary>Whatever the organiser counts in. <c>items</c>, <c>hours</c>, <c>boxes</c>.</summary>
    public string Unit { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public static PledgeDetail Create(decimal quantity, string unit, string description)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        return new PledgeDetail(
            quantity, unit.Trim().ToLowerInvariant(), description?.Trim() ?? string.Empty);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Quantity;
        yield return Unit;
        yield return Description;
    }
}
