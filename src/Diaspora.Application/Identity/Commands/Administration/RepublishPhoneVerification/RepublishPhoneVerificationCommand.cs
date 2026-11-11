using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Administration.RepublishPhoneVerification;

/// <summary>
/// Says again, to whoever is listening, that this member's phone is verified.
/// <para>
/// <b>For the accounts that were verified before anyone was listening.</b> Trust keeps its own
/// record of a member's verifications and had never been told about a phone, so its checklist
/// showed "Phone Linked — Verify" to everybody. The fix publishes
/// <c>UserPhoneVerifiedIntegrationEvent</c> whenever a phone is confirmed — but a member who
/// confirmed theirs last month confirmed it into silence, and <c>ConfirmPhoneVerified</c> is
/// idempotent, so they cannot simply do it again.
/// </para>
/// <para>
/// <b>It changes nothing in Identity.</b> No state moves, no security event is raised, nothing is
/// re-verified. The member's phone was already verified; this repeats the announcement, and the
/// consumers are idempotent, so running it twice costs nothing.
/// </para>
/// </summary>
[RequirePermission(IdentityPermissions.RepublishMemberFacts)]
public sealed record RepublishPhoneVerificationCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }
}
