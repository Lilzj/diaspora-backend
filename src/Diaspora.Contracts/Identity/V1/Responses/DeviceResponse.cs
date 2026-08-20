namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// A device registered to the member. The push token is never echoed back.
/// </summary>
public sealed record DeviceResponse
{
    public required Guid Id { get; init; }

    public required string DeviceIdentifier { get; init; }

    public required string FriendlyName { get; init; }

    public required string Platform { get; init; }

    public string? AppVersion { get; init; }

    public required bool HasPushToken { get; init; }

    public required bool IsTrusted { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }

    public required DateTimeOffset LastActiveAt { get; init; }
}
