namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Delivers a verification link to an address.
/// <para>
/// <b>No fallback, deliberately.</b> Recovery codes fall back to SMS when email fails, because a
/// member locked out is worse than a text message. A verification has nothing to fall back to: the
/// thing being proven is that this mailbox receives mail, and a text proves nothing about it.
/// </para>
/// </summary>
public interface IEmailVerificationSender
{
    /// <summary>False until an email transport is configured. The send route answers 503-shaped then.</summary>
    bool IsConfigured { get; }

    /// <summary>True when the provider accepted the message. Never logs the address or the link.</summary>
    Task<bool> SendAsync(EmailVerificationMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailVerificationMessage
{
    public required string Address { get; init; }

    /// <summary>The signed token. The sender builds the link from its own public base URL.</summary>
    public required string Token { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public string? Language { get; init; }
}
