using Diaspora.Platform.Services.Models;

namespace Diaspora.Platform.Services.Abstractions;

/// <summary>
/// <b>Not a notification path, and deliberately not one.</b> Declared in B1 before the platform had
/// a Communication module, never implemented, never called, and obsoleted in B1.4.1 rather than
/// implemented or deleted.
/// <para>
/// <b>The architectural rule this protects, established in B13.</b> Business modules publish
/// integration events. Only the Communication Platform creates notifications. A vertical that
/// creates one itself has quietly taken on channel preferences, quiet hours, digest batching,
/// localisation, deep links, delivery retries and the recipient's right to have turned the whole
/// category off — none of which it can see, and all of which B13 owns. The correct call from a
/// module is <c>IIntegrationEventPublisher.PublishAsync</c>, and what happens next is B13's
/// decision, taken against preferences and routing rules the module never has to know about.
/// </para>
/// <para>
/// <b>Why obsoleted rather than deleted.</b> Deleting it would remove the only thing standing where
/// the shortcut is: an empty namespace is an invitation to declare the interface again, and the
/// next author would have no way to know it had been considered and refused. Keeping it, with this
/// attribute, means the shortcut is discovered at the moment it is taken and answered with the
/// reason. The type also stays binary-compatible for anything already compiled against it —
/// obsoletion is a compiler diagnostic, not a change to the assembly's surface.
/// </para>
/// <para>
/// <b>Why obsoleted rather than implemented.</b> A thin adapter over B13's dispatcher would work,
/// and that is the problem: it would be a supported, discoverable, kernel-level way for any module
/// to create a notification directly, which is the single thing B13 exists to prevent. The platform
/// does not want this port to work. It wants it to be unavailable and to say why.
/// </para>
/// <para>
/// <b>What would have to happen to revive it.</b> Removing this attribute, which is a visible edit
/// to the kernel in a phase that says so, plus <c>DSP2003</c> — which treats naming this type from
/// a vertical as the notification bypass — plus
/// <c>The_dormant_notification_port_stays_dormant</c> and
/// <c>No_business_module_names_the_notification_write_path</c> in the architecture suite. Four
/// things, each of which asks the same question. None of them is hard to get past on purpose, and
/// all of them are hard to get past by accident, which is the whole design.
/// </para>
/// </summary>
/// <seealso href="../../../docs/platform/authorization-guide.md"/>
[Obsolete(
    "INotificationPublisher is not implemented and must not be. Business modules publish " +
    "integration events; only the Communication Platform (B13) creates notifications. Publish an " +
    "integration event through IIntegrationEventPublisher and let B13 decide whether, how and to " +
    "whom. See docs/notifications/architecture.md.",
    error: true,
    DiagnosticId = "DSP2004")]
public interface INotificationPublisher
{
    Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
