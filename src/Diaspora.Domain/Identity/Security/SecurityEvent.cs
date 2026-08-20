using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Security;

/// <summary>
/// An append-only record of something security-relevant happening to an account.
/// <para>
/// Immutable by construction: there is no mutator on this type, so the forensic
/// trail cannot be edited after the fact by any code path in the platform.
/// </para>
/// </summary>
public sealed class SecurityEvent : AggregateRoot<SecurityEventId>
{
    public const int MaxDescriptionLength = 512;

    /// <summary>
    /// Null when the subject could not be resolved — for example a sign-in attempt
    /// against a phone number that is not registered.
    /// </summary>
    public UserId? UserId { get; private set; }

    public SecurityEventType Type { get; private set; }
    public SecurityEventSeverity Severity { get; private set; }
    public string? Description { get; private set; }
    public SessionId? SessionId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceIdentifier { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Free-form JSON detail. Never carries credentials or code values.</summary>
    public string? MetadataJson { get; private set; }

    private SecurityEvent() { }

    private SecurityEvent(
        SecurityEventId id,
        UserId? userId,
        SecurityEventType type,
        SecurityEventSeverity severity,
        string? description,
        SessionId? sessionId,
        string? ipAddress,
        string? userAgent,
        string? deviceIdentifier,
        string? correlationId,
        string? metadataJson,
        DateTimeOffset occurredAt) : base(id)
    {
        UserId = userId;
        Type = type;
        Severity = severity;
        Description = description;
        SessionId = sessionId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        DeviceIdentifier = deviceIdentifier;
        CorrelationId = correlationId;
        MetadataJson = metadataJson;
        OccurredAt = occurredAt;
    }

    public static SecurityEvent Record(
        UserId? userId,
        SecurityEventType type,
        SecurityEventSeverity severity,
        DateTimeOffset occurredAt,
        string? description = null,
        SessionId? sessionId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceIdentifier = null,
        string? correlationId = null,
        string? metadataJson = null)
    {
        var trimmedDescription = description is null
            ? null
            : Guard.Against.MaxLength(description, MaxDescriptionLength, nameof(description));

        return new SecurityEvent(
            SecurityEventId.New(),
            userId,
            type,
            severity,
            trimmedDescription,
            sessionId,
            Truncate(ipAddress, 64),
            Truncate(userAgent, 400),
            Truncate(deviceIdentifier, 200),
            Truncate(correlationId, 64),
            metadataJson,
            occurredAt);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
