using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Memberships;

/// <summary>
/// The recognition that one settled payment bought one community membership.
/// <para>
/// <b>COMMUNITY-LIFE-GAP-003, and it adds no money movement.</b> The payment already happened
/// through the ordinary intent lifecycle; what did not exist was anything on this platform able to
/// say what that money was for once it had settled. This row is that sentence, and it is written
/// only after B15 has read the intent back and found it captured or settled.
/// </para>
/// <para>
/// <b>The payment intent is unique here, and that uniqueness is the concurrency control.</b> Two
/// simultaneous binding attempts for one payment race to insert the same
/// <see cref="PaymentIntentId"/>; the database refuses the second, and the handler answers the
/// loser with the binding that won rather than with an error — because both callers asked the same
/// question and there is one true answer. A handler-level "does one exist" check would leave the
/// window open between the check and the insert, which is exactly the window a double-tapped pay
/// button lands in.
/// </para>
/// <para>
/// <b>The membership is not unique.</b> A renewal is a second payment for the same membership and
/// must be expressible; only re-binding the <em>same</em> payment is a duplicate.
/// </para>
/// <para>
/// <b>Never deleted, never soft-deleted.</b> Nothing in this module is. A membership that lapses,
/// is cancelled or is refunded leaves this row alone: it records that money settled against a
/// membership, which stays true afterwards.
/// </para>
/// </summary>
public sealed class MembershipPaymentBinding
    : AggregateRoot<MembershipPaymentBindingId>, IAuditableEntity
{
    private MembershipPaymentBinding(
        MembershipPaymentBindingId id,
        Guid membershipId,
        PaymentIntentId paymentIntentId,
        Guid payerUserId,
        Money amount,
        DateTimeOffset settledAt,
        DateTimeOffset createdAt) : base(id)
    {
        MembershipId = membershipId;
        PaymentIntentId = paymentIntentId;
        PayerUserId = payerUserId;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        SettledAt = settledAt;
        CreatedAt = createdAt;
    }

    private MembershipPaymentBinding() { }

    /// <summary>B18's <c>ProgrammeMembership</c> identifier. Opaque here, exactly like every subject.</summary>
    public Guid MembershipId { get; private set; }

    /// <summary>Uniquely indexed. See the class summary.</summary>
    public PaymentIntentId PaymentIntentId { get; private set; } = default!;

    /// <summary>Who paid. Copied from the intent, never from the request.</summary>
    public Guid PayerUserId { get; private set; }

    /// <summary>
    /// What was captured, snapshotted at the moment of binding.
    /// <para>
    /// A snapshot rather than a join, because a later partial refund changes the intent and must not
    /// silently change the historical statement "this much bought this membership".
    /// </para>
    /// </summary>
    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>
    /// When the platform judged the money taken — the intent's settlement instant, or its capture
    /// instant when the provider has not confirmed settlement yet.
    /// </summary>
    public DateTimeOffset SettledAt { get; private set; }

    /// <summary>The organization whose programme this membership belongs to, when the seam knew it.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    /// <summary>The subject a membership payment intent must name to be bindable.</summary>
    public static SubjectReference SubjectFor(Guid membershipId) =>
        SubjectReference.Create(Enums.MonetizationSubjectType.CommunityMembership, membershipId);

    /// <summary>
    /// Records the binding and announces it.
    /// <para>
    /// <b>The event is raised here rather than by the handler</b>, so there is no path that writes
    /// the row without announcing it — and the announcement is what activates the membership. A
    /// handler that forgot the second half would produce a member who paid and never joined, which
    /// is the failure this whole gap exists to prevent.
    /// </para>
    /// </summary>
    public static MembershipPaymentBinding Record(
        MembershipPaymentBindingId id,
        Guid membershipId,
        PaymentIntentId paymentIntentId,
        Guid payerUserId,
        Money amount,
        DateTimeOffset settledAt,
        DateTimeOffset now,
        Guid? organizationId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(paymentIntentId);
        ArgumentNullException.ThrowIfNull(amount);

        if (membershipId == Guid.Empty)
        {
            throw new DomainException(
                "A binding with no membership is not a binding.",
                FinancialErrorCodes.MembershipSubjectMismatch);
        }

        var binding = new MembershipPaymentBinding(
            id, membershipId, paymentIntentId, payerUserId, amount, settledAt, now)
        {
            OrganizationId = organizationId
        };

        binding.RaiseDomainEvent(new MembershipPaymentSettledDomainEvent
        {
            BindingId = id,
            MembershipId = membershipId,
            PaymentIntentId = paymentIntentId,
            PayerUserId = payerUserId,
            OrganizationId = organizationId,
            AmountMinorUnits = amount.MinorUnits,
            Currency = amount.Currency,
            SettledAt = settledAt,
            BoundAt = now
        });

        return binding;
    }

    /// <summary>
    /// Re-announces a binding that already exists.
    /// <para>
    /// The reconciliation path. A capture that succeeded while the activation that should have
    /// followed did not leaves this row present and the membership short of it; replaying the event
    /// is how the membership catches up, and it is safe because activation is idempotent on the
    /// consuming side — a membership already active moves to active and does nothing.
    /// </para>
    /// </summary>
    public void Reannounce(DateTimeOffset now)
    {
        ReannouncedAt = now;
        ReannounceCount++;
        Revision++;
        ModifiedAt = now;

        RaiseDomainEvent(new MembershipPaymentSettledDomainEvent
        {
            BindingId = Id,
            MembershipId = MembershipId,
            PaymentIntentId = PaymentIntentId,
            PayerUserId = PayerUserId,
            OrganizationId = OrganizationId,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            SettledAt = SettledAt,
            BoundAt = now,
            IsReplay = true
        });
    }

    /// <summary>When the reconciliation sweep last re-announced this. Null until it has.</summary>
    public DateTimeOffset? ReannouncedAt { get; private set; }

    /// <summary>
    /// How many times it has. Bounded by the sweep so a membership nothing can activate — a deleted
    /// organization, a member who left — is not re-announced for ever.
    /// </summary>
    public int ReannounceCount { get; private set; }

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
}
