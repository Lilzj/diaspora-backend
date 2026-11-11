using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Media;

/// <summary>
/// Attaches an uploaded asset to the guide, by reference.
/// <para>
/// A reference and never a URL, per ADR-005: the reference outlives storage migrations, CDN
/// changes and signing schemes, and a URL copied into a guide outlives none of them. Idempotent
/// on the pair (media, role), which is what makes the endpoint safely retryable from a flaky
/// upload screen.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.EditOwn)]
[Auditable]
public sealed record AttachGuideMediaCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required AttachGuideMediaRequest Request { get; init; }
}
