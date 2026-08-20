using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Authentication.Providers;

/// <summary>
/// Phone number and password.
/// <para>
/// Carries forward, verbatim, the two security properties B2's login handler was careful
/// about: an unknown number still pays for one password hash against a dummy value, so
/// response latency does not reveal whether the number is registered; and every failure
/// returns the same coarse <see cref="UserErrors.InvalidCredentials"/>, so the endpoint
/// cannot be turned into an account-enumeration oracle. Moving the logic behind the
/// provider interface changes nothing a caller can observe.
/// </para>
/// </summary>
public sealed class PasswordAuthenticationProvider : IAuthenticationProvider
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IClock _clock;

    public PasswordAuthenticationProvider(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ISecurityEventWriter securityEvents,
        IIdentityPolicyProvider policies,
        IClock clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _securityEvents = securityEvents;
        _policies = policies;
        _clock = clock;
    }

    public AuthenticationMethod Method => AuthenticationMethod.Password;

    public async Task<Result<AuthenticatedMember>> AuthenticateAsync(
        AuthenticationCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var now = _clock.UtcNow;
        var password = credentials.Password ?? string.Empty;

        if (!PhoneNumber.TryCreate(credentials.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            // Pay the hashing cost even on a malformed number so the timing matches a real
            // attempt, then refuse indistinguishably.
            _passwordHasher.Verify(_passwordHasher.DummyHash, password);
            return Result.Failure<AuthenticatedMember>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            _passwordHasher.Verify(_passwordHasher.DummyHash, password);
            return Result.Failure<AuthenticatedMember>(UserErrors.InvalidCredentials);
        }

        var verification = _passwordHasher.Verify(user.PasswordHash, password);
        if (verification == PasswordVerificationOutcome.Failed)
        {
            user.RecordFailedLogin(now, _policies.Current.Lockout);
            _users.Update(user);

            if (user.Security.IsLockedOut(now))
            {
                await _securityEvents.WriteAsync(new SecurityEventDraft
                {
                    UserId = user.Id,
                    Type = SecurityEventType.TemporaryLockoutTriggered,
                    Severity = SecurityEventSeverity.Warning,
                    Description = $"Temporary lockout after {user.Security.FailedLoginAttempts} failed attempts."
                }, cancellationToken);
            }

            return Result.Failure<AuthenticatedMember>(UserErrors.InvalidCredentials);
        }

        if (verification == PasswordVerificationOutcome.SucceededNeedsRehash)
        {
            // The plaintext is in hand exactly once per sign-in; the only moment an older
            // hash can be upgraded without asking the member to do anything.
            user.UpgradePasswordHash(_passwordHasher.Hash(password));
            _users.Update(user);
        }

        return Result.Success(new AuthenticatedMember(user, Method));
    }
}
