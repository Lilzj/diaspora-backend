using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// Published when a member asks to be verified. The Trust &amp; Verification Engine
/// (Phase B3) is the intended consumer; it replies through the trust decision endpoint.
/// </summary>
public sealed record UserVerificationRequestedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string CountryOfResidence { get; init; }

    public required string Nationality { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
