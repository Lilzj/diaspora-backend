using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Verification;

/// <summary>
/// Where the guide's verification stands, for the author who submitted it.
/// <para>
/// Permissioned rather than public, because this is the one verification surface that carries
/// the <em>reason</em>. A badge is a public fact and rides on the guide itself; "revoked for
/// inaccuracy" is feedback to the author, and publishing it beside somebody's name would be a
/// defamation risk dressed as transparency.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.SubmitVerification)]
public sealed record GetGuideVerificationStatusQuery : IQuery<GuideVerificationStatusResponse>
{
    public required Guid GuideId { get; init; }
}
