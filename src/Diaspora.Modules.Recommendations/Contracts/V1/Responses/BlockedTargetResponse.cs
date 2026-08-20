namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>Something the member said never to show them again. Permanent, unlike a dismissal.</summary>
public sealed record BlockedTargetResponse
{
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    public required DateTimeOffset BlockedAt { get; init; }
}
