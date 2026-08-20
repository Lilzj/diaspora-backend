using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A knowledge category was created, renamed, archived or restored.
/// <para>
/// One event for the whole lifecycle, distinguished by <see cref="Change"/>. The alternative —
/// four event types — would give four handlers that all do the same two things: relay a contract
/// and invalidate the navigation cache.
/// </para>
/// </summary>
public sealed record KnowledgeCategoryChangedDomainEvent : DomainEvent
{
    public required KnowledgeCategoryId KnowledgeCategoryId { get; init; }

    /// <summary>The stable key. Carried on the event because consumers key off it, not off the id.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>One of the verbs in <see cref="KnowledgeChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>The guide category keys this node draws content from.</summary>
    public required IReadOnlyList<string> SourceCategoryKeys { get; init; }

    /// <summary>The curator, when there was one. Seeding has none.</summary>
    public UserId? ChangedBy { get; init; }
}
