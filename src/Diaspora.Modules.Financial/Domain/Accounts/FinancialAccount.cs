using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Accounts;

/// <summary>
/// A financial identity: one owner, one place their money lives.
/// <para>
/// <b>Created for every verified member automatically, and for nobody else.</b> Verification is
/// Trust's word, arriving as a published contract; this module never asks. An unverified member has
/// no financial identity, which is not a limitation but the point — the first thing a fraud
/// programme wants is for money to be impossible before somebody is known.
/// </para>
/// <para>
/// <b>The account is not the balance.</b> It owns no money and has no amount on it anywhere. It
/// exists so that a member, an organization and a provider can each hold several wallets — a primary
/// balance, an escrow pot, granted credit — without three sets of rules for whose they are.
/// </para>
/// <para>
/// <b><see cref="OwnerId"/> is a foreign identifier this module never resolves.</b> For a personal
/// account it is Identity's user id; for an organization, B12's organization id; for a provider,
/// B7's provider id. Nothing here joins to any of them.
/// </para>
/// </summary>
public sealed class FinancialAccount : AggregateRoot<FinancialAccountId>, IAuditableEntity
{
    private FinancialAccount(
        FinancialAccountId id,
        Guid ownerId,
        FinancialAccountType accountType,
        string primaryCurrency,
        string displayName,
        DateTimeOffset createdAt) : base(id)
    {
        OwnerId = ownerId;
        AccountType = accountType;
        PrimaryCurrency = primaryCurrency;
        DisplayName = displayName;
        Status = FinancialAccountStatus.Pending;
        CreatedAt = createdAt;
    }

    private FinancialAccount() { }

    /// <summary>The identifier in the module that owns the thing. Opaque here.</summary>
    public Guid OwnerId { get; private set; }

    public FinancialAccountType AccountType { get; private set; }

    public FinancialAccountStatus Status { get; private set; }

    /// <summary>
    /// The currency this owner's first wallet was opened in. A default, not a restriction — an
    /// account may hold a wallet in any currency the catalogue knows.
    /// </summary>
    public string PrimaryCurrency { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, when it is known. Used to resolve country-scoped policy.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>What a statement is addressed to. Free text, kept for support.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Why the account is restricted, when it is. Null otherwise.</summary>
    public string? RestrictionReason { get; private set; }

    /// <summary>
    /// The platform's own account. Exactly one exists and it is the counterparty to everything.
    /// </summary>
    public bool IsPlatformAccount { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static FinancialAccount Open(
        FinancialAccountId id,
        Guid ownerId,
        FinancialAccountType accountType,
        string primaryCurrency,
        string displayName,
        DateTimeOffset createdAt,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryCurrency);

        return new FinancialAccount(
            id,
            ownerId,
            accountType,
            primaryCurrency.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(displayName) ? "Account" : displayName.Trim(),
            createdAt)
        {
            CountryCode = NormalizeCountry(countryCode)
        };
    }

    /// <summary>
    /// The platform's own account, opened once by the seeder.
    /// <para>
    /// Active from the moment it exists: nothing can create it but the seeder, and a pending
    /// platform account would make every other transaction on the platform fail its counterparty
    /// check for as long as somebody forgot to activate it.
    /// </para>
    /// </summary>
    public static FinancialAccount OpenPlatform(
        FinancialAccountId id, string primaryCurrency, DateTimeOffset createdAt)
    {
        var account = Open(
            id, Guid.Empty, FinancialAccountType.Platform, primaryCurrency, "Diaspora Platform", createdAt);

        account.IsPlatformAccount = true;
        account.Status = FinancialAccountStatus.Active;

        return account;
    }

    public void Activate(DateTimeOffset now)
    {
        if (Status is FinancialAccountStatus.Active or FinancialAccountStatus.Closed)
        {
            return;
        }

        Status = FinancialAccountStatus.Active;
        RestrictionReason = null;
        Touch(now);
    }

    /// <summary>
    /// Puts the account under review. Wallets keep their balances and refuse new debits.
    /// <para>
    /// Refuses on the platform account rather than returning false. Restricting the counterparty to
    /// every transaction would stop the platform, and a caller who could ignore the answer is a
    /// caller who will.
    /// </para>
    /// </summary>
    public void Restrict(string reason, DateTimeOffset now)
    {
        if (IsPlatformAccount)
        {
            throw new DomainException(
                "The platform account is the counterparty to every transaction and cannot be restricted.",
                FinancialErrorCodes.AccountImmutable);
        }

        Status = FinancialAccountStatus.Restricted;
        RestrictionReason = string.IsNullOrWhiteSpace(reason) ? "Under review." : reason.Trim();
        Touch(now);
    }

    public void Close(DateTimeOffset now)
    {
        if (IsPlatformAccount)
        {
            throw new DomainException(
                "The platform account cannot be closed.", FinancialErrorCodes.AccountImmutable);
        }

        Status = FinancialAccountStatus.Closed;
        Touch(now);
    }

    public void SetCountry(string? countryCode, DateTimeOffset now)
    {
        var normalized = NormalizeCountry(countryCode);

        if (string.Equals(CountryCode, normalized, StringComparison.Ordinal))
        {
            return;
        }

        CountryCode = normalized;
        Touch(now);
    }

    /// <summary>Whether money may leave this account at all.</summary>
    public bool CanDebit => Status == FinancialAccountStatus.Active;

    /// <summary>Whether money may arrive. A restricted account still accepts refunds.</summary>
    public bool CanCredit => Status is FinancialAccountStatus.Active or FinancialAccountStatus.Restricted;

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

    private static string? NormalizeCountry(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
