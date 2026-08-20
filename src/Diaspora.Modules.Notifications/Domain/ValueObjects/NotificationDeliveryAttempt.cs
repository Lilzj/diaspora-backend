using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// One attempt by one channel, kept forever.
/// <para>
/// <b>Held flat on the notification rather than nested inside the delivery record, and that is a
/// persistence decision made deliberately.</b> An owned collection inside an owned collection is
/// legal in EF Core and is a reliable source of composite-key surprises; a flat collection keyed on
/// the notification with the channel as a column answers every question the nested shape would —
/// "what happened on push", "how many times did we try" — with a model that migrates without
/// argument.
/// </para>
/// <para>
/// The attempt log is what makes a failure explicable. A delivery that merely says "failed" tells an
/// operator nothing during an incident; six rows saying <c>Timeout, Timeout, Rejected</c> tell them
/// the provider changed something an hour ago.
/// </para>
/// </summary>
public sealed class NotificationDeliveryAttempt : ValueObject
{
    private NotificationDeliveryAttempt(
        Guid attemptId,
        NotificationChannelKind channel,
        int attemptNumber,
        bool succeeded,
        string? reasonCode,
        string? detail,
        string? providerMessageId,
        DateTimeOffset attemptedAt)
    {
        AttemptId = attemptId;
        Channel = channel;
        AttemptNumber = attemptNumber;
        Succeeded = succeeded;
        ReasonCode = reasonCode;
        Detail = detail;
        ProviderMessageId = providerMessageId;
        AttemptedAt = attemptedAt;
    }

    private NotificationDeliveryAttempt() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid AttemptId { get; private set; }

    public NotificationChannelKind Channel { get; private set; }

    /// <summary>One-based. The first attempt is 1, not 0, because an operator reads this.</summary>
    public int AttemptNumber { get; private set; }

    public bool Succeeded { get; private set; }

    /// <summary>A stable machine code from the channel. Null on success.</summary>
    public string? ReasonCode { get; private set; }

    /// <summary>The transport's own words, clipped. Never shown to a member.</summary>
    public string? Detail { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public static NotificationDeliveryAttempt ForSuccess(
        Guid attemptId,
        NotificationChannelKind channel,
        int attemptNumber,
        string? providerMessageId,
        DateTimeOffset attemptedAt) =>
        new(attemptId, channel, attemptNumber, true, null, null, Clip(providerMessageId), attemptedAt);

    public static NotificationDeliveryAttempt ForFailure(
        Guid attemptId,
        NotificationChannelKind channel,
        int attemptNumber,
        string reasonCode,
        string? detail,
        DateTimeOffset attemptedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        return new NotificationDeliveryAttempt(
            attemptId, channel, attemptNumber, false, Clip(reasonCode), Clip(detail), null, attemptedAt);
    }

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= NotificationLimits.MaxReasonLength
            ? trimmed
            : trimmed[..NotificationLimits.MaxReasonLength];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AttemptId;
        yield return Channel;
        yield return AttemptNumber;
        yield return Succeeded;
        yield return ReasonCode;
        yield return Detail;
        yield return ProviderMessageId;
        yield return AttemptedAt;
    }
}
