using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Catalog;

/// <summary>A category as a contributor declares it.</summary>
public sealed record NotificationCategorySeed
{
    /// <summary>Lower-cased and stable. Every notification and preference stores this.</summary>
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public NotificationPriority DefaultPriority { get; init; } = NotificationPriority.Normal;

    /// <summary>
    /// True for anything a member must not be able to silence.
    /// <para>
    /// Security is the obvious one. A mandatory category renders its switch disabled with a reason,
    /// rather than offering a control that quietly does nothing.
    /// </para>
    /// </summary>
    public bool IsMandatory { get; init; }

    public bool DigestByDefault { get; init; }

    /// <summary>Lower sorts first on the preference screen.</summary>
    public int Position { get; init; }
}
