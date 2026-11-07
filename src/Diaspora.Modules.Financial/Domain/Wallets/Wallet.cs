using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Wallets;

/// <summary>
/// A balance in one currency — and a projection of the ledger, never a source of truth.
/// <para>
/// <b>There is no way to set a balance on this type.</b> Every balance property has a private
/// setter and the only public member that moves one is <see cref="Apply"/>, which takes a
/// <see cref="LedgerEntry"/>. No <c>Credit</c>, no <c>Debit</c>, no <c>Deposit</c>, no
/// <c>AdjustBalance</c>. That is the whole architectural claim of this phase expressed as a type,
/// and an architecture test asserts the absence by name — because "the wallet is a projection" is
/// exactly the kind of rule the platform's handbook says survives until the next author unless
/// something fails the build.
/// </para>
/// <para>
/// <b>Three balances, because "you have £40" is often not true.</b> Available is spendable now;
/// pending is arriving and not yet settled; reserved is committed against something. A single
/// balance would have to pick one of those three meanings and be wrong about the other two at the
/// moment a member most cares.
/// </para>
/// <para>
/// <b><see cref="EntryCount"/> is both the count and the next sequence number.</b> One field rather
/// than two that can disagree — B14's handbook entry about one fact in two places applies exactly
/// here, and a gap in a statement is the kind of defect that is only ever found by a customer.
/// </para>
/// <para>
/// <b>Rebuildable.</b> <see cref="RebuildFrom"/> replays entries from zero and produces the same
/// numbers. Deleting every wallet row and replaying the ledger is a supported recovery; deleting the
/// ledger is not, and nothing in this module can.
/// </para>
/// </summary>
public sealed class Wallet : AggregateRoot<WalletId>, IAuditableEntity
{
    private Wallet(
        WalletId id,
        FinancialAccountId accountId,
        Guid ownerId,
        FinancialAccountType ownerType,
        WalletType walletType,
        string currency,
        int exponent,
        DateTimeOffset createdAt) : base(id)
    {
        AccountId = accountId;
        OwnerId = ownerId;
        OwnerType = ownerType;
        WalletType = walletType;
        Currency = currency;
        Exponent = exponent;
        Status = WalletStatus.Pending;
        CreatedAt = createdAt;
    }

    private Wallet() { }

    public FinancialAccountId AccountId { get; private set; } = default!;

    /// <summary>Denormalised from the account so a wallet query needs no join. Never changes.</summary>
    public Guid OwnerId { get; private set; }

    public FinancialAccountType OwnerType { get; private set; }

    public WalletType WalletType { get; private set; }

    /// <summary>ISO 4217, upper case. A wallet holds exactly one currency, for ever.</summary>
    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public WalletStatus Status { get; private set; }

    /// <summary>Spendable now. Derived from the ledger.</summary>
    public long AvailableMinorUnits { get; private set; }

    /// <summary>Arriving, not yet settled. Derived from the ledger.</summary>
    public long PendingMinorUnits { get; private set; }

    /// <summary>Committed against something. Derived from the ledger.</summary>
    public long ReservedMinorUnits { get; private set; }

    /// <summary>Everything that has ever arrived. Never decreases.</summary>
    public long LifetimeCreditMinorUnits { get; private set; }

    /// <summary>Everything that has ever left. Never decreases.</summary>
    public long LifetimeDebitMinorUnits { get; private set; }

    /// <summary>Entries applied so far, and therefore the next sequence number minus one.</summary>
    public long EntryCount { get; private set; }

    public DateTimeOffset? LastEntryAt { get; private set; }

    /// <summary>
    /// Why the wallet is in its current status: why it was frozen or closed, or why a freeze was
    /// lifted. Null when nobody needed to say (a wallet simply opened).
    /// </summary>
    public string? StatusReason { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Whether this wallet's balance may go below zero.
    /// <para>
    /// <b>True for exactly one wallet on the platform: the clearing wallet.</b> It represents the
    /// boundary with the outside world, and its balance is by construction the negative of
    /// everything the platform has taken in. In a double-entry system one nominal account has to be
    /// allowed to run that way or nothing balances; making it the platform's own, rather than
    /// relaxing the rule everywhere, is what keeps every member-facing balance provably non-negative.
    /// </para>
    /// </summary>
    public bool AllowsNegativeBalance =>
        WalletType == WalletType.Clearing && OwnerType == FinancialAccountType.Platform;

    /// <summary>
    /// Spendable now, as money.
    /// <para>
    /// Clamped at zero, which is a no-op for every wallet but the clearing one — see
    /// <see cref="AllowsNegativeBalance"/>. The clearing wallet is a staff-only nominal account whose
    /// meaningful reading is <see cref="AvailableMinorUnits"/>, and no member-facing surface serves
    /// one.
    /// </para>
    /// </summary>
    public Money Available => AsMoney(AvailableMinorUnits);

    public Money Pending => AsMoney(PendingMinorUnits);

    public Money Reserved => AsMoney(ReservedMinorUnits);

    public Money LifetimeCredits => AsMoney(LifetimeCreditMinorUnits);

    public Money LifetimeDebits => AsMoney(LifetimeDebitMinorUnits);

    /// <summary>The next sequence number an entry against this wallet must take.</summary>
    public long NextSequence => EntryCount + 1;

    public static Wallet Open(
        WalletId id,
        FinancialAccountId accountId,
        Guid ownerId,
        FinancialAccountType ownerType,
        WalletType walletType,
        string currency,
        DateTimeOffset createdAt,
        int exponent = Money.DefaultExponent)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(accountId);

        var normalized = Money.NormalizeCurrency(currency)
            ?? throw new ArgumentException(
                $"'{currency}' is not an ISO 4217 currency code.", nameof(currency));

        var wallet = new Wallet(
            id, accountId, ownerId, ownerType, walletType, normalized, exponent, createdAt);

        wallet.RaiseDomainEvent(new WalletCreatedDomainEvent
        {
            WalletId = id,
            AccountId = accountId,
            OwnerId = ownerId,
            OwnerType = ownerType,
            WalletType = walletType,
            Currency = normalized,
            CreatedAt = createdAt
        });

        return wallet;
    }

    /// <summary>
    /// Makes the wallet usable.
    /// <para>
    /// <b>The reason is kept when one is given (7 Nov 2026).</b> Opening a wallet needs none; lifting a
    /// freeze, by staff or because the member's appeal was upheld, says why, and the member reads it.
    /// </para>
    /// </summary>
    public void Activate(DateTimeOffset now, string? reason = null)
    {
        if (Status == WalletStatus.Active)
        {
            return;
        }

        if (Status == WalletStatus.Closed)
        {
            throw new DomainException(
                "A closed wallet cannot be reopened. Open a new one.",
                FinancialErrorCodes.WalletClosed);
        }

        Transition(WalletStatus.Active, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), now);
    }

    public void Freeze(string reason, DateTimeOffset now)
    {
        if (Status == WalletStatus.Closed)
        {
            return;
        }

        Transition(
            WalletStatus.Frozen,
            string.IsNullOrWhiteSpace(reason) ? "Frozen." : reason.Trim(),
            now);
    }

    /// <summary>
    /// Closes the wallet.
    /// <para>
    /// Refuses while anything is held. A closed wallet with a reserved balance is money nobody can
    /// reach: the release path checks the status, and a release that could be refused leaves a hold
    /// in place for ever — which is the failure B1.3 recorded as strictly worse than the one the
    /// guard prevents.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The reason is kept (7 Nov 2026). It used to be discarded, so a member whose wallet was closed
    /// by staff could never learn why.
    /// </remarks>
    public void Close(DateTimeOffset now, string? reason = null)
    {
        if (ReservedMinorUnits > 0 || PendingMinorUnits > 0)
        {
            throw new DomainException(
                "A wallet holding reserved or pending funds cannot be closed. Release them first.",
                FinancialErrorCodes.WalletHasHeldFunds);
        }

        Transition(WalletStatus.Closed, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), now);
    }

    /// <summary>
    /// Folds one ledger entry into the projection.
    /// <para>
    /// <b>The only balance mutator on this type.</b> It refuses an entry for another wallet or in
    /// another currency, and it refuses one whose recorded <c>BalanceBefore</c> disagrees with what
    /// this projection currently holds — because an entry appended against a stale read is precisely
    /// the corruption a projection cannot detect later.
    /// </para>
    /// </summary>
    public void Apply(LedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.WalletId != Id)
        {
            throw new InvalidOperationException(
                "That ledger entry belongs to a different wallet.");
        }

        if (!string.Equals(entry.Currency, Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"A {entry.Currency} entry cannot be applied to a {Currency} wallet.");
        }

        var before = BalanceOf(entry.Bucket);

        if (entry.BalanceBeforeMinorUnits != before)
        {
            throw new InvalidOperationException(
                $"Entry {entry.Sequence} was computed against a {entry.Bucket} balance of "
                + $"{entry.BalanceBeforeMinorUnits}; this wallet holds {before}.");
        }

        SetBucket(entry.Bucket, entry.BalanceAfterMinorUnits);

        if (entry.Direction == LedgerDirection.Credit)
        {
            LifetimeCreditMinorUnits += entry.AmountMinorUnits;
        }
        else
        {
            LifetimeDebitMinorUnits += entry.AmountMinorUnits;
        }

        EntryCount = Math.Max(EntryCount, entry.Sequence);
        LastEntryAt = entry.OccurredAt;
        Revision++;
    }

    /// <summary>
    /// Replays the wallet from zero.
    /// <para>
    /// The recovery path, and the proof the projection is one: every number on this aggregate can be
    /// produced again from the entries alone. Lifetime totals are recomputed too, so a rebuild after
    /// a partial failure does not double them.
    /// </para>
    /// </summary>
    public void RebuildFrom(IEnumerable<LedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        AvailableMinorUnits = 0;
        PendingMinorUnits = 0;
        ReservedMinorUnits = 0;
        LifetimeCreditMinorUnits = 0;
        LifetimeDebitMinorUnits = 0;
        EntryCount = 0;
        LastEntryAt = null;

        foreach (var entry in entries.OrderBy(entry => entry.Sequence))
        {
            Apply(entry);
        }
    }

    /// <summary>Whether this wallet can currently give up an amount from a bucket.</summary>
    public bool CanDebit(Money amount, LedgerBucket bucket = LedgerBucket.Available)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return Status == WalletStatus.Active
            && string.Equals(amount.Currency, Currency, StringComparison.Ordinal)
            && (AllowsNegativeBalance || BalanceOf(bucket) >= amount.MinorUnits);
    }

    /// <summary>Whether money may arrive. A frozen wallet still accepts refunds.</summary>
    public bool CanCredit(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return Status is WalletStatus.Active or WalletStatus.Frozen
            && string.Equals(amount.Currency, Currency, StringComparison.Ordinal);
    }

    public long BalanceOf(LedgerBucket bucket) => bucket switch
    {
        LedgerBucket.Available => AvailableMinorUnits,
        LedgerBucket.Pending => PendingMinorUnits,
        LedgerBucket.Reserved => ReservedMinorUnits,
        _ => 0
    };

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private Money AsMoney(long minorUnits) =>
        Money.FromMinorUnits(Math.Max(0, minorUnits), Currency, Exponent);

    private void SetBucket(LedgerBucket bucket, long value)
    {
        switch (bucket)
        {
            case LedgerBucket.Available:
                AvailableMinorUnits = value;
                break;
            case LedgerBucket.Pending:
                PendingMinorUnits = value;
                break;
            case LedgerBucket.Reserved:
                ReservedMinorUnits = value;
                break;
            default:
                throw new InvalidOperationException($"Unknown ledger bucket '{bucket}'.");
        }
    }

    private void Transition(WalletStatus status, string? reason, DateTimeOffset now)
    {
        var previous = Status;

        Status = status;
        StatusReason = reason;
        Revision++;
        ModifiedAt = now;

        RaiseDomainEvent(new WalletStatusChangedDomainEvent
        {
            WalletId = Id,
            AccountId = AccountId,
            OwnerId = OwnerId,
            OwnerType = OwnerType,
            PreviousStatus = previous,
            Status = status,
            Currency = Currency,
            Reason = reason,
            ChangedAt = now
        });
    }
}
