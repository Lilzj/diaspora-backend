namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// The state of an outstanding one-time code. Never carries the code itself, in any
/// environment.
/// </summary>
public sealed record OtpChallengeResponse
{
    public required string PhoneNumber { get; init; }

    public required string Purpose { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required int AttemptsRemaining { get; init; }

    public required int ResendsRemaining { get; init; }

    /// <summary>Seconds the client must wait before a resend is accepted.</summary>
    public required int ResendAvailableInSeconds { get; init; }
}
