using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide moved to a different category.
/// <para>
/// Carries both ends of the move, because that is what a consumer branches on: a category
/// browse cache invalidates two lists, not one, and an event carrying only the new key would
/// make every consumer keep its own copy of the old one.
/// </para>
/// </summary>
public sealed record GuideCategoryChangedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>The catalog key before the change.</summary>
    public required string PreviousCategoryKey { get; init; }

    /// <summary>The catalog key after it.</summary>
    public required string CurrentCategoryKey { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
