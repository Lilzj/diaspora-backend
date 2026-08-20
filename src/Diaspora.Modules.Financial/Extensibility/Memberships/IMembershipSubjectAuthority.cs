namespace Diaspora.Modules.Financial.Extensibility.Memberships;

/// <summary>
/// The little B15 is allowed to know about a community membership it is taking money for.
/// <para>
/// <b>Three facts, and none of them is money.</b> Who holds the membership, which body runs it, and
/// whether it is waiting to be paid for. B15 needs the first to refuse a caller binding somebody
/// else's membership, the second for the audit trail, and the third so a payment cannot be bound to
/// a membership that was never approved.
/// </para>
/// <para>
/// <b>Why a seam and not a module reference.</b> B15 references no vertical, which is what lets it
/// be the sole owner of money without becoming a dependency of every module that sells something.
/// The membership is B18's aggregate; B18 composes after B15 and registers the implementation, and
/// the direction of the arrow never reverses.
/// </para>
/// <para>
/// <b>The default is refusal.</b> With no implementation registered, <see cref="DescribeAsync"/>
/// returns null and the binding is refused as "no such membership" — not allowed through on the
/// grounds that nothing objected. A capability model whose failure mode is "grant" is not a
/// capability model, and this one moves money.
/// </para>
/// </summary>
public interface IMembershipSubjectAuthority
{
    /// <summary>
    /// Describes the membership, or returns null when it does not exist or cannot be seen.
    /// </summary>
    Task<MembershipSubjectFacts?> DescribeAsync(
        Guid membershipId, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the owning module says about one membership.
/// <para>
/// Deliberately not the membership: no tier, no price, no history, no name. Each of those would be
/// a field B15 could start deciding with, and B15 decides nothing about a membership.
/// </para>
/// </summary>
public sealed record MembershipSubjectFacts
{
    /// <summary>The member who holds it. The caller must be this person.</summary>
    public required Guid MemberUserId { get; init; }

    /// <summary>B12's organization, for the audit trail.</summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// Whether the membership is in a state that is waiting for a payment.
    /// <para>
    /// <b>The owning module's judgement, and B15 does not second-guess it.</b> Approved,
    /// payment-failed and renewal-due are all waiting; active, rejected and cancelled are not.
    /// Encoding that ladder here would be a second copy of somebody else's state machine.
    /// </para>
    /// </summary>
    public required bool IsAwaitingPayment { get; init; }
}
