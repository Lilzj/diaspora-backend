using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Integration;

/// <summary>
/// Binding a captured payment to the membership it bought.
/// <para>
/// <b>The only thing on this platform that decides whether a payment bought a membership.</b> The
/// decision is B15's because the payment is B15's: it checks that the intent is captured, that the
/// caller holds <c>PaymentsManage</c> at <c>AccessTier.Verified</c>, that the amount matches the
/// tier and that the same intent has not already been bound. B18 owns the membership and asks.
/// </para>
/// <para>
/// <b>A service rather than a command, deliberately, and this seam does not change that.</b>
/// <c>DSP1001</c> refuses a handler that can reach another handler in the process, so the shared
/// behaviour is a service B18's handler injects — and it stays one. What this interface changes is
/// only that B18 names a published seam instead of the concrete class behind it, which is what let
/// it name <c>Financial.Application.Memberships</c> for as long as it did.
/// </para>
/// <para>
/// <b>The caller's own write is separate, on purpose.</b> The binder saves its own; B18's handler
/// does its own <c>SaveChanges</c> afterwards and carries no <c>[Transactional]</c>. One scope
/// around both would put two modules' writes in one transaction and make a membership activation
/// able to roll back a financial record.
/// </para>
/// </summary>
public interface IMembershipPayments
{
    /// <summary>
    /// Binds a captured payment intent to a membership, or says why it cannot be bound.
    /// <para>
    /// Idempotent: binding the same intent twice answers with the existing binding rather than
    /// taking the money's word for it a second time.
    /// </para>
    /// </summary>
    Task<Result<MembershipPaymentBindingResponse>> BindAsync(
        Guid membershipId, Guid paymentIntentId, CancellationToken cancellationToken = default);
}
