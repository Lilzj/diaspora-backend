namespace Diaspora.Modules.CommunityLife.Extensibility.Catalog;

/// <summary>A kind of event a contributor supplies.</summary>
public sealed record EventCategoryDescriptor
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconName { get; init; }

    public string? GroupName { get; init; }

    /// <summary>
    /// The search facets this category belongs to — <c>religious</c>, <c>cultural</c>,
    /// <c>business</c>, <c>student</c>. A category may be in several: a student cultural night is
    /// both.
    /// </summary>
    public IReadOnlyList<string> Facets { get; init; } = [];

    public int Order { get; init; }

    /// <summary>Whether events of this kind take responses by default. False for a Sunday service.</summary>
    public bool DefaultsToRsvp { get; init; } = true;
}
