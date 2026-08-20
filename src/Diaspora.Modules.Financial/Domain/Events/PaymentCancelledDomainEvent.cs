using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>Somebody stopped paying, or nobody finished in time.</summary>
public sealed record PaymentCancelledDomainEvent : DomainEvent
{
    public required PaymentIntentId PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>True when it lapsed rather than being cancelled by somebody.</summary>
    public required bool Expired { get; init; }

    public required DateTimeOffset CancelledAt { get; init; }
}
