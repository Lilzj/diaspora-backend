using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Catalog;

/// <summary>Adds an event category or restates one. Staff only.</summary>
[Authorize(Permission = CommunityLifePermissions.ManageCatalogue)]
[Auditable]
public sealed record UpsertEventCategoryCommand : ICommand<EventCategoryResponse>
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconName { get; init; }

    public string? GroupName { get; init; }

    public IReadOnlyList<string>? Facets { get; init; }

    public int Order { get; init; }

    public bool? DefaultsToRsvp { get; init; }

    public bool? IsActive { get; init; }
}
