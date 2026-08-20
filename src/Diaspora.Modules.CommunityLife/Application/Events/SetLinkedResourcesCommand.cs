using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Replaces what an event points at elsewhere on the platform.
/// <para>
/// Career-fair vacancies and vendor booths live here, held as typed and deliberately unvalidated
/// references the client resolves against the owning module. Jobs and Marketplace publish no
/// integration directory, and inventing one for a link would make two verticals depend on each
/// other to render an anchor.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.EventEditOwn)]
[Auditable]
public sealed record SetLinkedResourcesCommand : ICommand<EventResponse>
{
    public required Guid EventId { get; init; }

    public required SetLinkedResourcesRequest Request { get; init; }
}
