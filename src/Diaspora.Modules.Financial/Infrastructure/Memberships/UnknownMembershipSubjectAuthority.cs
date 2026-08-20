using Diaspora.Modules.Financial.Extensibility.Memberships;

namespace Diaspora.Modules.Financial.Infrastructure.Memberships;

/// <summary>
/// What the platform knows about a community membership when nothing has told it.
/// <para>
/// <b>Nothing, and that is the answer that keeps money safe.</b> A deployment that has not composed
/// the module owning memberships gets "no such membership" from the binding route rather than a
/// container resolution failure — and rather than a permissive default, which would let a payment
/// activate a membership on nobody's authority.
/// </para>
/// <para>
/// Registered by hand alongside the seam rather than discovered by the assembly scanner. A null
/// object that sits in the same scanned set as the implementation it stands in for will eventually
/// be registered after it and win, silently — see the note in <c>FinancialModule</c>.
/// </para>
/// </summary>
public sealed class UnknownMembershipSubjectAuthority : IMembershipSubjectAuthority
{
    public Task<MembershipSubjectFacts?> DescribeAsync(
        Guid membershipId, CancellationToken cancellationToken = default) =>
        Task.FromResult<MembershipSubjectFacts?>(null);
}
