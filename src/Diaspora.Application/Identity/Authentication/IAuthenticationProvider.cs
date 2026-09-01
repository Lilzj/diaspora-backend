using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Authentication;

/// <summary>
/// The credentials a caller presents. A superset across methods; each provider reads only
/// the fields it needs. Password reads <see cref="Password"/>; phone-OTP reads
/// <see cref="Code"/>; a future OAuth provider would read a field added here.
/// </summary>
/// <param name="PhoneNumber">The claimed identity. Every current method is phone-first.</param>
public sealed record AuthenticationCredentials(string PhoneNumber)
{
    public string? Password { get; init; }

    public string? Code { get; init; }

    /// <summary>
    /// The sign-in identifier for the password method.
    /// </summary>
    /// <remarks>
    /// Sign-in moved from a phone one-time code to an email address and a password. The
    /// phone number stays on this record because the <b>one-time code</b> method still
    /// uses it — that is what a code proves, and phone verification is the one place an
    /// SMS earns its cost.
    /// </remarks>
    public string? Email { get; init; }
}

/// <summary>
/// Whom a provider authenticated, and how.
/// </summary>
/// <param name="User">The member. The caller runs the access-state checks and issues tokens.</param>
/// <param name="Method">The method that succeeded, for the security-event trail.</param>
public sealed record AuthenticatedMember(User User, AuthenticationMethod Method);

/// <summary>
/// Verifies one kind of credential and returns the member it belongs to.
/// <para>
/// A provider answers only "who is this?", never "what may they do?" — the access-state
/// gate and token issuance are shared across every method and live in the handler that
/// calls the provider. That division is what makes the methods interchangeable: swapping
/// password for a one-time code changes how identity is proven and nothing downstream of
/// it.
/// </para>
/// <para>
/// Adding Google, Apple or a magic link means adding an implementation of this interface
/// and registering it. No existing provider, handler or endpoint changes — the registry
/// finds the new one by its <see cref="Method"/>.
/// </para>
/// </summary>
public interface IAuthenticationProvider
{
    AuthenticationMethod Method { get; }

    /// <summary>
    /// Verifies the credentials. Returns the member on success, or a deliberately
    /// non-committal failure — providers must not let the caller distinguish "no such
    /// account" from "wrong credential", so account enumeration stays impossible.
    /// </summary>
    Task<Result<AuthenticatedMember>> AuthenticateAsync(
        AuthenticationCredentials credentials,
        CancellationToken cancellationToken = default);
}
