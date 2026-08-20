using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// The money is the platform's.
/// <para>
/// <b>The event every downstream grant hangs off.</b> A placement activates on capture, a
/// subscription starts on capture, a donation is receipted on capture — because capture is the first
/// moment the platform actually has the money.
/// </para>
/// </summary>
public sealed record PaymentCapturedIntegrationEvent : IntegrationEvent
{
    public required Guid PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? Provider { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }
}
