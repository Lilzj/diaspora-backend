namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// What one channel did with one message.
/// <para>
/// Staff-facing. It names no words and no subject — a delivery record answers "did it go", which is
/// a question an operator may ask about anybody, while the message itself is only ever the
/// recipient's.
/// </para>
/// </summary>
public sealed record NotificationDeliveryResponse
{
    public required Guid Id { get; init; }

    public required string Channel { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset DeliverAfter { get; init; }

    public DateTimeOffset? NextAttemptAt { get; init; }

    public required int AttemptCount { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Why it failed or was suppressed. A stable machine code.</summary>
    public string? ReasonCode { get; init; }

    public string? ProviderMessageId { get; init; }
}
