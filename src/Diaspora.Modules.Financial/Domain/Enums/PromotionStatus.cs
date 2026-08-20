namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Whether a promotion can currently be redeemed.</summary>
public enum PromotionStatus
{
    Draft = 1,
    Active = 2,
    Paused = 3,

    /// <summary>Its usage limit is spent.</summary>
    Exhausted = 4,

    Expired = 5
}
