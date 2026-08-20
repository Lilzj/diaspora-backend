namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Whether money has actually gone back.</summary>
public enum RefundStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3
}
