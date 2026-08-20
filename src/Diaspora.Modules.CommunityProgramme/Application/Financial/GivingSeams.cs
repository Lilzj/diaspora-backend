using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.Financial.Extensibility.Campaigns;
using Diaspora.Modules.Financial.Extensibility.Memberships;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityProgramme.Application.Financial;

/// <summary>
/// Answers B15's three questions about a membership it is taking money for.
/// <para>
/// <b>The arrow points one way and this is what keeps it that way.</b> B15 references no vertical;
/// B18 already references B15 for the currency catalogue, composes after it, and implements this
/// seam. Every fact B15 needs to decide whether a payment may be bound to a membership comes from
/// here — and nothing about the membership beyond those three facts crosses the boundary.
/// </para>
/// <para>
/// <b>"Awaiting payment" is this module's judgement, not a list B15 keeps.</b> Approved,
/// payment-pending, payment-failed and renewal-due are the four states a membership can be paid for
/// from; encoding that ladder in B15 would be a second copy of a state machine that lives here.
/// </para>
/// </summary>
public sealed class MembershipSubjectAuthority : IMembershipSubjectAuthority
{
    private readonly IProgrammeMembershipRepository _memberships;

    public MembershipSubjectAuthority(IProgrammeMembershipRepository memberships) =>
        _memberships = memberships;

    public async Task<MembershipSubjectFacts?> DescribeAsync(
        Guid membershipId, CancellationToken cancellationToken = default)
    {
        var membership = await _memberships.GetAsync(
            ProgrammeMembershipId.From(membershipId), cancellationToken);

        return membership is null
            ? null
            : new MembershipSubjectFacts
            {
                MemberUserId = membership.MemberUserId.Value,
                OrganizationId = membership.OrganizationId,
                IsAwaitingPayment = MembershipActivation.IsAwaitingPayment(membership.Status)
            };
    }
}

/// <summary>
/// Answers B15's question about who may run an organization's fundraising.
/// <para>
/// <b>A projection of B12's role edge, through B18's capability set, and not a third opinion.</b>
/// <c>canManageCampaigns</c> is one of the ten flags B18 already computes from B12's ladder; a
/// campaign asks for it exactly as an announcement or a poll does. Without this registered B15
/// refuses every organization campaign, which is the safe direction.
/// </para>
/// </summary>
public sealed class CampaignOrganizerAuthority : ICampaignOrganizerAuthority
{
    private readonly CommunityProgrammeAccessService _access;
    private readonly IOrganizationDirectory _organizations;

    public CampaignOrganizerAuthority(
        CommunityProgrammeAccessService access, IOrganizationDirectory organizations)
    {
        _access = access;
        _organizations = organizations;
    }

    public async Task<bool> CanManageCampaignsAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        // Asked of the caller's own capabilities rather than of the userId parameter, and the two
        // are the same person on every path that reaches here — B15 passes the authenticated
        // caller. Guarding on it explicitly stops a future caller passing somebody else's id and
        // being told what they may do.
        if (_access.CallerId?.Value != userId)
        {
            return false;
        }

        return await _access.HoldsAsync(
            organizationId, OrganizationCapability.ManageCampaigns, cancellationToken);
    }

    public async Task<string?> DescribeAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        (await _organizations.FindAsync(organizationId, cancellationToken))?.Name;
}

/// <summary>
/// Activates a membership once B15 says its payment settled. COMMUNITY-LIFE-GAP-003.
/// <para>
/// <b>This is the only thing that turns a paid membership on, and an HTTP 200 is not.</b> The
/// binding route asks B15 to verify and record the settlement; B15 publishes the fact; this
/// activates. A capture that timed out, a client that closed the app, an instance that died between
/// the two — none of them leaves a member who paid and did not join, because the sweep re-announces
/// and this runs again.
/// </para>
/// <para>
/// <b>Idempotent, and it has to be.</b> At-least-once is the delivery contract and the
/// reconciliation sweep replays deliberately. <c>MoveTo</c> a state the membership already holds
/// succeeds and does nothing, so a redelivery adds no history entry and raises no second event.
/// </para>
/// <para>
/// <b>Refuses to activate a membership the payment was not for.</b> The event names a membership and
/// a payer; if the membership belongs to somebody else, something has gone wrong upstream and the
/// safe answer is to log and stop rather than to grant.
/// </para>
/// </summary>
public sealed class MembershipSettlementConsumer
    : IntegrationEventHandler<MembershipPaymentSettledIntegrationEvent>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MembershipSettlementConsumer> _logger;

    public MembershipSettlementConsumer(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<MembershipSettlementConsumer> logger)
    {
        _memberships = memberships;
        _programmes = programmes;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        MembershipPaymentSettledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var membership = await _memberships.GetAsync(
            ProgrammeMembershipId.From(integrationEvent.MembershipId), cancellationToken);

        if (membership is null)
        {
            _logger.LogWarning(
                "A settled membership payment named membership {Membership}, which does not exist.",
                integrationEvent.MembershipId);

            return;
        }

        if (membership.MemberUserId.Value != integrationEvent.PayerUserId)
        {
            _logger.LogError(
                "A settled payment by {Payer} named membership {Membership}, which belongs to "
                + "somebody else. Not activating.",
                integrationEvent.PayerUserId,
                integrationEvent.MembershipId);

            return;
        }

        var now = _clock.UtcNow;

        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var tier = programme?.FindTier(membership.TierId);

        var activated = MembershipActivation.Activate(
            membership, tier?.RenewalMonths, () => _guids.NewSequentialGuid(), now);

        if (!activated)
        {
            return;
        }

        membership.AttachPayment(integrationEvent.PaymentIntentId, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// The one place a settled payment becomes an active membership.
/// <para>
/// <b>Written once because two callers need it and they must not disagree.</b> The binding route
/// activates synchronously so the client gets a definite answer, and the settlement consumer
/// activates on the event so a failure of the first is repaired. Two copies of the ladder below
/// would be two chances to get <c>PaymentFailed</c> wrong.
/// </para>
/// <para>
/// <b><c>PaymentFailed</c> takes two hops and that is the transition table's rule, not a
/// workaround.</b> A membership whose card was declined goes back through <c>PaymentPending</c>,
/// because "we are trying again" is a state the member is shown and skipping it would lose it from
/// the history.
/// </para>
/// </summary>
internal static class MembershipActivation
{
    /// <summary>The four states a membership can be paid for from.</summary>
    public static bool IsAwaitingPayment(ProgrammeMembershipStatus status) =>
        status is ProgrammeMembershipStatus.Approved
            or ProgrammeMembershipStatus.PaymentPending
            or ProgrammeMembershipStatus.PaymentFailed
            or ProgrammeMembershipStatus.RenewalDue;

    /// <summary>
    /// Moves a membership to active, through whatever intermediate state the table requires.
    /// <para>
    /// Returns false when nothing was done — already active, or in a state from which no route to
    /// active exists. False is not an error: a redelivered settlement finds an active membership,
    /// and that is the idempotency this whole path depends on.
    /// </para>
    /// </summary>
    public static bool Activate(
        ProgrammeMembership membership,
        int? billingPeriodMonths,
        Func<Guid> historyEntryIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(historyEntryIds);

        if (membership.Status == ProgrammeMembershipStatus.Active)
        {
            return false;
        }

        if (!IsAwaitingPayment(membership.Status))
        {
            return false;
        }

        if (membership.Status == ProgrammeMembershipStatus.PaymentFailed)
        {
            var retried = membership.MoveTo(
                ProgrammeMembershipStatus.PaymentPending,
                historyEntryIds(),
                now,
                MembershipActor.System);

            if (retried.IsFailure)
            {
                return false;
            }
        }

        var activated = membership.MoveTo(
            ProgrammeMembershipStatus.Active,
            historyEntryIds(),
            now,
            MembershipActor.System,
            renewalMonths: billingPeriodMonths);

        return activated.IsSuccess;
    }
}
