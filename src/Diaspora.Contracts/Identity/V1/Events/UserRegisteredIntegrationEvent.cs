using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// Published when a Diaspora account is created. Other modules use this to seed their
/// own read models; they must not query Identity's tables directly.
/// </summary>
public sealed record UserRegisteredIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string FullName { get; init; }

    public required string Nationality { get; init; }

    public required string CountryOfResidence { get; init; }

    public required string PreferredLanguage { get; init; }
}
