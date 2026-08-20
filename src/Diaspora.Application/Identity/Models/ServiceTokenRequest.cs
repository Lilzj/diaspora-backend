namespace Diaspora.Application.Identity.Models;

/// <summary>
/// A request to mint a token for a trusted platform service.
/// </summary>
public sealed record ServiceTokenRequest
{
    /// <summary>
    /// The calling service, for example "trust-engine". Becomes the token subject and
    /// appears in every audit record the service's calls produce.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Exactly the permissions this service needs. Service tokens are issued narrow — the
    /// Trust Engine's carries the trust-decision permission and nothing else.
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }

    /// <summary>
    /// How long the token lives. Kept short; service callers are expected to re-mint.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }

    public string? CorrelationId { get; init; }
}
