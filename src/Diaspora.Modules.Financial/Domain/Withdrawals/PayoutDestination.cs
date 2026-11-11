using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Withdrawals;

/// <summary>
/// Where a member's money goes when it leaves — recorded as a handle, never as a credential.
/// <para>
/// <b>There is no property on this type that could hold an account number, an IBAN, a sort code or a
/// card number, and that is the security guarantee.</b> The platform stores a kind, a masked label a
/// person can recognise, and whatever opaque token the settlement provider issued for the real
/// details it holds. A guarantee expressed as an absent field survives the next author; a guarantee
/// expressed as "we are careful not to log it" does not.
/// </para>
/// <para>
/// <b>Verification is the provider's, not the platform's.</b> B15 records whether a destination has
/// been verified and refuses to pay out to one that has not; what verification consisted of — a micro
/// deposit, a name match, an open-banking handshake — belongs to whoever performed it.
/// </para>
/// <para>
/// <b>Removal is a state, not a delete.</b> A withdrawal from six months ago still names its
/// destination, and a row that vanished would leave that reference dangling. The rest of this module
/// is soft-delete-free for the same reason.
/// </para>
/// </summary>
public sealed class PayoutDestination : AggregateRoot<PayoutDestinationId>, IAuditableEntity
{
    private PayoutDestination(
        PayoutDestinationId id,
        Guid ownerUserId,
        FinancialAccountId accountId,
        PayoutDestinationKind kind,
        string label,
        string currency,
        DateTimeOffset createdAt) : base(id)
    {
        OwnerUserId = ownerUserId;
        AccountId = accountId;
        Kind = kind;
        Label = label;
        Currency = currency;
        CreatedAt = createdAt;
    }

    private PayoutDestination() { }

    public Guid OwnerUserId { get; private set; }

    public FinancialAccountId AccountId { get; private set; } = default!;

    public PayoutDestinationKind Kind { get; private set; }

    /// <summary>
    /// What a person recognises it by. "Barclays ••••4417", "MTN ••••882". Produced by whoever
    /// verified the destination; this module never derives one from a number it does not hold.
    /// </summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>The currency this destination can receive. A wallet pays out to a match or nothing.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// The settlement provider's own token for the real details. Opaque here, and the only thing
    /// this platform can offer a provider when asking it to send money.
    /// </summary>
    public string? ProviderToken { get; private set; }

    public string? Provider { get; private set; }

    public string? CountryCode { get; private set; }

    /// <summary>False until somebody who could check has. Nothing pays out to an unverified one.</summary>
    public bool IsVerified { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>True once the member removed it. Never deleted — see the class summary.</summary>
    public bool IsRemoved { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether money may be sent here right now.</summary>
    public bool IsUsable => IsVerified && !IsRemoved;

    public static PayoutDestination Register(
        PayoutDestinationId id,
        Guid ownerUserId,
        FinancialAccountId accountId,
        PayoutDestinationKind kind,
        string label,
        string currency,
        DateTimeOffset createdAt,
        string? provider = null,
        string? providerToken = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var normalized = ValueObjects.Money.NormalizeCurrency(currency)
            ?? throw new DomainException(
                $"'{currency}' is not an ISO 4217 currency code.",
                FinancialErrorCodes.CurrencyInvalid);

        var destination = new PayoutDestination(
            id, ownerUserId, accountId, kind, label.Trim(), normalized, createdAt)
        {
            Provider = Trimmed(provider),
            ProviderToken = Trimmed(providerToken),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };

        // BSEC-007, 10 Nov 2026. This aggregate raised nothing, ever, so a new payout destination
        // was the quietest change on the platform — and the loudest signal that an account has been
        // taken over. The label, not the token: what travels is the masked label the member would
        // recognise, and nothing a provider would accept.
        destination.RaiseDomainEvent(new PayoutDestinationRegisteredDomainEvent
        {
            DestinationId = destination.Id,
            OwnerUserId = destination.OwnerUserId,
            Kind = destination.Kind,
            Label = destination.Label,
            Currency = destination.Currency,
            Provider = destination.Provider,
            CountryCode = destination.CountryCode,
            RegisteredAt = createdAt
        });

        return destination;
    }

    /// <summary>Records that somebody who could check did.</summary>
    public void Verify(DateTimeOffset now, string? provider = null, string? providerToken = null)
    {
        if (IsRemoved)
        {
            throw new DomainException(
                "A removed payout destination cannot be verified. Add it again.",
                FinancialErrorCodes.DestinationNotFound);
        }

        IsVerified = true;
        VerifiedAt = now;
        Provider = Trimmed(provider) ?? Provider;
        ProviderToken = Trimmed(providerToken) ?? ProviderToken;
        Touch(now);
    }

    public void Remove(DateTimeOffset now)
    {
        if (IsRemoved)
        {
            return;
        }

        IsRemoved = true;
        RemovedAt = now;

        // The token goes with the destination. Keeping a provider handle for something the member
        // asked to remove would be keeping the ability to send money there.
        ProviderToken = null;
        Touch(now);
    }

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

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
