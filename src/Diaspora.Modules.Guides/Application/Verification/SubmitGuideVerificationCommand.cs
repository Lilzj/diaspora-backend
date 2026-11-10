using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Verification;

/// <summary>
/// Raises a verification claim for a published guide.
/// <para>
/// <b>Asks. It does not grant.</b> Everything this command can do is record that a claim went
/// out. The badge arrives later, on the deciding system's integration event, through the
/// projector that owns <c>Guide.ApplyVerificationDecision</c> — which is the reason a module
/// cannot verify itself.
/// </para>
/// <para>
/// Gated at <see cref="GuidePermissions.SubmitVerification"/>, whose tier floor is Verified: a
/// claim to authoritative knowledge is exactly the kind of assertion the access tiers exist to
/// make members earn.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.SubmitVerification)]
[Auditable]
public sealed record SubmitGuideVerificationCommand : ICommand<GuideVerificationStatusResponse>
{
    public required Guid GuideId { get; init; }

    public required SubmitGuideVerificationRequest Request { get; init; }
}
