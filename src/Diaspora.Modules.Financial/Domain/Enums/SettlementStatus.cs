namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// A future payout run.
/// <para>
/// Declared so the extension seam has a vocabulary. Nothing in this module moves a settlement past
/// <see cref="Pending"/>, and an architecture test asserts no settlement engine ships here.
/// </para>
/// </summary>
public enum SettlementStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}
