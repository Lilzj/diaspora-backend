using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>The issuer has reserved the funds. Nothing has been taken yet.</summary>
public sealed record PaymentAuthorizedDomainEvent : DomainEvent
{
    public required PaymentIntentId PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? Provider { get; init; }

    public string? ExternalReference { get; init; }

    public required DateTimeOffset AuthorizedAt { get; init; }
}
