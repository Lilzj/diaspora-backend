using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// A template was retired.
/// <para>
/// Internal, and it invalidates the same cache publication warms. Messages already sent from it are
/// untouched — they carry their own rendered words, which is the whole reason rendering happens once.
/// </para>
/// </summary>
public sealed record TemplateDeactivatedDomainEvent : DomainEvent
{
    public required NotificationTemplateId TemplateId { get; init; }

    public required string TemplateKey { get; init; }

    public required DateTimeOffset DeactivatedAt { get; init; }
}
