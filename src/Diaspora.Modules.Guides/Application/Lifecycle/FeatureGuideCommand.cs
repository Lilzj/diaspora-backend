using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Gives a published guide editorial placement.
/// <para>
/// A staff act, never an author's. <see cref="GuidePermissions.Feature"/> is role-scoped rather
/// than tier-scoped, per the access-tier semantics: a tier measures progression, a role grants
/// privilege, and featuring is unambiguously a privilege.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Feature)]
[Auditable]
public sealed record FeatureGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }
}
