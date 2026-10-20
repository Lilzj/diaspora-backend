using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Administration.RepublishEmailVerification;

/// <summary>
/// Says again, to whoever is listening, that this member's email address is verified.
/// <para>
/// <b>For the accounts verified between 5 and 20 Oct 2026</b>, when email verification worked and
/// published nothing. <c>ConfirmEmailVerified</c> is idempotent, so those members cannot cause
/// the announcement by verifying again. The same repair the phone has
/// (<c>RepublishPhoneVerificationCommand</c>), under the same permission.
/// </para>
/// <para>
/// <b>It changes nothing in Identity</b>, and Trust's consumer is idempotent, so running it on an
/// account that is already mirrored costs nothing. A member whose email is not verified is refused.
/// </para>
/// </summary>
[Authorize(Permission = IdentityPermissions.RepublishMemberFacts)]
public sealed record RepublishEmailVerificationCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }
}
