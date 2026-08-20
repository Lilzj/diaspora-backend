using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Routing;

/// <summary>A routing rule as a contributor declares it.</summary>
public sealed record NotificationRuleSeed
{
    public required string Key { get; init; }

    /// <summary>
    /// The published contract's type name.
    /// <para>
    /// Written as <c>nameof(SomeIntegrationEvent)</c> by every shipped contributor, so a contract
    /// rename is a compile error rather than a rule that silently stops matching.
    /// </para>
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>Which participant this rule addresses — <c>candidate</c>, <c>employer</c>, <c>owner</c>.</summary>
    public string Audience { get; init; } = "recipient";

    public required string TemplateKey { get; init; }

    public required string CategoryKey { get; init; }

    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    public DeliveryPolicy Policy { get; init; } = DeliveryPolicy.Immediate;

    /// <summary>The channels proposed. Narrowed by preferences; widened only by Critical.</summary>
    public IReadOnlyList<NotificationChannelKind> Channels { get; init; } =
        [NotificationChannelKind.InApp];

    /// <summary>
    /// When to send, relative to the moment the event names.
    /// <para>
    /// Negative is the interesting case: <c>-PT24H</c> on a viewing reminder means "the day before",
    /// expressed without any code in this module knowing what a viewing is.
    /// </para>
    /// </summary>
    public TimeSpan? Offset { get; init; }

    public TimeSpan? RetentionPeriod { get; init; }

    /// <summary>How long two messages from the same source count as one. Null disables de-duplication.</summary>
    public TimeSpan? DeduplicationWindow { get; init; }
}
