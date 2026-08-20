namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A kind of event, as the picker renders one.</summary>
public sealed record EventCategoryResponse
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconName { get; init; }

    public string? GroupName { get; init; }

    /// <summary>The search facets this category belongs to. What a faceted browse filters on.</summary>
    public IReadOnlyList<string> Facets { get; init; } = [];

    public required int Order { get; init; }

    public required bool DefaultsToRsvp { get; init; }

    public required int EventCount { get; init; }
}
