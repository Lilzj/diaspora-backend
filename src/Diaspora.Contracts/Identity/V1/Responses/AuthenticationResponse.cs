namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// The single payload every successful authentication returns: who you are, what you
/// can reach, and the tokens to reach it with.
/// <para>
/// The access decision travels with the tokens so a client never has to infer where to
/// send the member from the raw access state.
/// </para>
/// </summary>
public sealed record AuthenticationResponse
{
    public required TokenPairResponse Tokens { get; init; }

    public required UserProfileResponse User { get; init; }

    public required AccessDecisionResponse Access { get; init; }

    public required Guid SessionId { get; init; }
}
