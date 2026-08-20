using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A notice category was created, renamed, archived or restored.
/// <para>
/// The vocabulary a member subscribes to. B13's routing rules and a client's preference screen both
/// key off <see cref="Key"/>, which is why the key is on the event rather than only the identifier:
/// a consumer maintaining a preference list has no way to resolve one into the other.
/// </para>
/// </summary>
public sealed record NoticeCategoryChangedDomainEvent : DomainEvent
{
    public required NoticeCategoryId NoticeCategoryId { get; init; }

    /// <summary>The stable key. Lower-case kebab, unique, never rewritten.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>One of the verbs in <see cref="IntelligenceChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>What a notice in this category is worth by default, before an editor overrides it.</summary>
    public required NoticeSeverity DefaultSeverity { get; init; }

    public UserId? ChangedBy { get; init; }
}
