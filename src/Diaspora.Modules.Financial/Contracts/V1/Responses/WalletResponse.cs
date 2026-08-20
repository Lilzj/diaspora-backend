namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One balance, as its owner sees it.
/// <para>
/// <b>Five figures rather than one, because "you have £40" is often not true.</b> Available is
/// spendable now; pending is arriving and not yet settled; reserved is committed against something.
/// A single balance would have to pick one of those meanings and be wrong about the other two at
/// exactly the moment somebody cared.
/// </para>
/// <para>
/// <b>Every figure is derived from the ledger.</b> Nothing on this platform sets a balance.
/// </para>
/// </summary>
public sealed record WalletResponse
{
    public required Guid WalletId { get; init; }

    public required Guid AccountId { get; init; }

    /// <summary><c>Primary</c>, <c>Escrow</c>, <c>Revenue</c>, <c>Promotional</c>.</summary>
    public required string WalletType { get; init; }

    /// <summary><c>Personal</c>, <c>Organization</c>, <c>Provider</c>, <c>Business</c>.</summary>
    public required string OwnerType { get; init; }

    /// <summary><c>Pending</c>, <c>Active</c>, <c>Frozen</c>, <c>Closed</c>.</summary>
    public required string Status { get; init; }

    public required string Currency { get; init; }

    public required MoneyResponse Available { get; init; }

    public required MoneyResponse Pending { get; init; }

    public required MoneyResponse Reserved { get; init; }

    public required MoneyResponse LifetimeCredits { get; init; }

    public required MoneyResponse LifetimeDebits { get; init; }

    /// <summary>How many ledger entries this wallet has. Also the newest sequence number.</summary>
    public required long EntryCount { get; init; }

    public DateTimeOffset? LastEntryAt { get; init; }

    /// <summary>Why the wallet is frozen, when it is.</summary>
    public string? StatusReason { get; init; }
}
