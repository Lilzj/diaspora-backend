using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Knowledge.V1.Events;

/// <summary>
/// A knowledge category was created, renamed, archived or restored.
/// <para>
/// <b>This is the navigation vocabulary of the directory, not the guide filing taxonomy.</b> A
/// guide is filed under a guide category and always has been; a knowledge category is what a
/// country page shows as a row, in what order, under what localized label, drawing from one or
/// more guide categories. Consumers that cache navigation — a client tab bar, a sitemap generator
/// — key off this event; consumers that file content still key off the guide category events B8
/// publishes.
/// </para>
/// </summary>
public sealed record KnowledgeCategoryUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid KnowledgeCategoryId { get; init; }

    /// <summary>The stable key. Lower-case kebab, unique, never rewritten.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>One of <c>created</c>, <c>updated</c>, <c>archived</c>, <c>restored</c>.</summary>
    public required string Change { get; init; }

    /// <summary>Whether the category may still be placed on a country directory.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The guide category keys this navigation node draws content from.</summary>
    public IReadOnlyList<string> SourceCategoryKeys { get; init; } = [];

    public Guid? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
