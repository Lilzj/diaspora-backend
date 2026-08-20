using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A payment did not go through.
/// <para>
/// Carries a failure code and a message the platform may show. <b>Both come from the gateway and
/// neither is interpreted here</b> — a code table mapping every provider's decline reasons onto a
/// platform vocabulary would be a provider dependency in a module that has none, and it would be
/// wrong for the next provider.
/// </para>
/// </summary>
public sealed record PaymentFailedDomainEvent : DomainEvent
{
    public required PaymentIntentId PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public required bool IsRetryable { get; init; }

    public required DateTimeOffset FailedAt { get; init; }
}
