using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Signs and reads the link that moves a member's sign-in address to a new one.
/// <para>
/// <b>Signed, not stored</b>, like <see cref="IEmailVerificationLinks"/>, and under a key of its own
/// so neither kind of link can be redeemed as the other. A link carries everything the change
/// needs and everything that should stop it:
/// </para>
/// <list type="bullet">
///   <item>the <b>address it moves from</b> — confirming compares it with the address on the
///   account, so a link dies the moment any change completes, and opening it twice does nothing;</item>
///   <item>the <b>security stamp</b> when it was issued — a password change or reset rotates the
///   stamp, so the member who receives the "someone asked to change your email" notice cancels
///   every outstanding link by changing their password;</item>
///   <item>the <b>session that asked</b> — the one session left signed in when the change completes.</item>
/// </list>
/// </summary>
public interface IEmailChangeLinks
{
    /// <summary>Mints a token that moves <paramref name="request"/>'s member to its new address.</summary>
    (string Token, DateTimeOffset ExpiresAt) Create(EmailChangeClaim request);

    /// <summary>Reads a token. Null when it is malformed, forged or expired. Never throws.</summary>
    EmailChangeClaim? Redeem(string? token);
}

/// <summary>What a change link says. Addresses are normalised to lower case.</summary>
public sealed record EmailChangeClaim(
    UserId UserId,
    string FromAddress,
    string ToAddress,
    int SecurityStamp,
    SessionId? RequestedBySession);
