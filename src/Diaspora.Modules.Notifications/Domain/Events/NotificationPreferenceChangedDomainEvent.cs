using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// One section of a member's communication preferences moved.
/// <para>
/// Raised per section rather than per save, so a consumer watching for members opting out entirely
/// can tell which switch did it. The relay collapses several of these into a single published event
/// per unit of work — a member who changes four switches on one screen is one fact to everybody
/// outside this module.
/// </para>
/// </summary>
public sealed record NotificationPreferenceChangedDomainEvent : DomainEvent
{
    public required NotificationPreferenceId PreferenceId { get; init; }

    public required UserId UserId { get; init; }

    /// <summary>
    /// <c>Channels</c>, <c>Categories</c>, <c>QuietHours</c>, <c>Digest</c>, <c>Language</c>,
    /// <c>TimeZone</c>, <c>DoNotDisturb</c> or <c>Marketing</c>.
    /// </summary>
    public required string Section { get; init; }

    public required bool IsFullyOptedOut { get; init; }
}
