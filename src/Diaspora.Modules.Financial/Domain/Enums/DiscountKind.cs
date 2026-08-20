namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>How a promotion reduces what is owed.</summary>
public enum DiscountKind
{
    Percentage = 1,
    FixedAmount = 2,

    /// <summary>The whole price, whatever it is.</summary>
    Free = 3
}
