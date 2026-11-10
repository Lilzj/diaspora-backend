using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Media;

/// <summary>
/// Removes a media reference from the guide.
/// <para>
/// Route values rather than a body: the asset and the role together are the reference's
/// identity, and there is nothing else to say. Idempotent — detaching what is not there
/// succeeds, so a client retrying a timed-out request is not punished for it.
/// </para>
/// <para>
/// The asset itself is not deleted. Media owns its lifetime, and a guide that stopped pointing
/// at a photograph has said nothing about whether anything else still does.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.EditOwn)]
[Auditable]
public sealed record DetachGuideMediaCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required Guid MediaId { get; init; }

    /// <summary>A <c>GuideMediaRole</c> name — the other half of the reference's identity.</summary>
    public required string Role { get; init; }
}
