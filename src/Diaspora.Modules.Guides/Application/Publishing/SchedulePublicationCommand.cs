using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Publishing;

/// <summary>
/// Sets a draft to publish itself at a future instant.
/// <para>
/// Drafts only and future only. Scheduling records an intention on the guide; the hosted
/// publisher is what acts on it, and it runs the same publish-then-snapshot sequence the manual
/// command does — so a scheduled publication is not a lesser publication.
/// </para>
/// <para>
/// Gated at <see cref="GuidePermissions.Schedule"/> rather than <c>Publish</c>: the two are
/// separable capabilities, and a deployment that wants editorial control over timing can grant
/// one without the other.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Schedule)]
[Auditable]
public sealed record SchedulePublicationCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required ScheduleGuidePublicationRequest Request { get; init; }
}
