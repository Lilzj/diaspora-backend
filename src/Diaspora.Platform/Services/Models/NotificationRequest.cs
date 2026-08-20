namespace Diaspora.Platform.Services.Models;

/// <summary>
/// <b>The payload of a port that is not a path.</b> The only thing that ever named this type was
/// <see cref="Abstractions.INotificationPublisher"/>, which B1.4.1 obsoleted; it is obsoleted with
/// it, so the shape cannot be picked up and used to build the same bypass under another name.
/// <para>
/// A module that wants somebody notified publishes an integration event. B13's routing rules decide
/// the recipients, its templates decide the wording in the reader's language, and its preferences
/// decide whether the notification is created at all — none of which fits in a title and a body.
/// See <see cref="Abstractions.INotificationPublisher"/> for the reasoning and
/// <c>docs/notifications/architecture.md</c> for the model.
/// </para>
/// </summary>
[Obsolete(
    "NotificationRequest is the payload of the unimplemented INotificationPublisher and is not a " +
    "notification path. Publish an integration event through IIntegrationEventPublisher and let " +
    "the Communication Platform (B13) decide whether, how and to whom. See " +
    "docs/notifications/architecture.md.",
    error: true,
    DiagnosticId = "DSP2004")]
public sealed record NotificationRequest
{
    public required string RecipientId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public NotificationChannel Channel { get; init; } = NotificationChannel.InApp;
    public string? ImageUrl { get; init; }
    public string? ActionType { get; init; }
    public Dictionary<string, string>? ActionPayload { get; init; }
}
