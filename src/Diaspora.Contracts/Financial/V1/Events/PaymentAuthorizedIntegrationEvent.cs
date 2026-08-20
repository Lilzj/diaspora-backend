using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// An issuer reserved the funds. Nothing has been taken.
/// <para>
/// <b>Nothing should be granted on this event.</b> An authorisation can still fail; everything the
/// platform hands out hangs off <see cref="PaymentCapturedIntegrationEvent"/> instead.
/// </para>
/// </summary>
public sealed record PaymentAuthorizedIntegrationEvent : IntegrationEvent
{
    public required Guid PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>Whatever the registered adapter calls itself. Never interpreted.</summary>
    public string? Provider { get; init; }

    public required DateTimeOffset AuthorizedAt { get; init; }
}
