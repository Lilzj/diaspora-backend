namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Where a financial action entered the platform.</summary>
public enum FinancialSource
{
    Api = 1,
    Webhook = 2,
    System = 3,
    Operator = 4,
    Migration = 5
}
