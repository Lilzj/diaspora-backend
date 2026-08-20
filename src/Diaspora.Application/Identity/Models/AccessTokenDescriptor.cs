namespace Diaspora.Application.Identity.Models;

/// <summary>
/// A minted access token and the metadata a client needs to manage it.
/// </summary>
public sealed record AccessTokenDescriptor
{
    public required string Token { get; init; }

    public required string TokenId { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public int ExpiresInSeconds => Math.Max(0, (int)(ExpiresAt - IssuedAt).TotalSeconds);
}
