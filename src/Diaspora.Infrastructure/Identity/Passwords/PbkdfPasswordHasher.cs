using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Infrastructure.Identity.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Passwords;

/// <summary>
/// Password hashing built on ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>.
/// <para>
/// Using the framework hasher rather than hand-rolling PBKDF2 buys the versioned hash
/// format, the fixed-time comparison and the rehash-needed signal — all of which are easy
/// to get subtly wrong and impossible to fix retroactively once passwords are stored.
/// </para>
/// <para>
/// The iteration count is configurable and is expected to rise over time. Members are
/// migrated to the stronger parameters as they sign in, through the
/// <see cref="PasswordVerificationOutcome.SucceededNeedsRehash"/> path.
/// </para>
/// </summary>
public sealed class PbkdfPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Fixed input behind <see cref="DummyHash"/>. Its value is irrelevant — it is never
    /// compared for a real credential, only to make the failure path cost the same as the
    /// success path.
    /// </summary>
    private const string DummyPassword = "diaspora-timing-equalisation-placeholder";

    private readonly PasswordHasher<object> _hasher;
    private readonly object _hashOwner = new();
    private readonly Lazy<PasswordHash> _dummyHash;

    public PbkdfPasswordHasher(IOptions<PasswordPolicyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _hasher = new PasswordHasher<object>(Microsoft.Extensions.Options.Options.Create(
            new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                IterationCount = options.Value.HashIterations
            }));

        // Computed once and cached. Deriving it per failed sign-in would make the
        // no-account path measurably slower than the wrong-password path, which is the
        // opposite of what it exists to achieve.
        _dummyHash = new Lazy<PasswordHash>(
            () => PasswordHash.Create(_hasher.HashPassword(_hashOwner, DummyPassword)),
            isThreadSafe: true);
    }

    public PasswordHash DummyHash => _dummyHash.Value;

    public PasswordHash Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return PasswordHash.Create(_hasher.HashPassword(_hashOwner, password));
    }

    public PasswordVerificationOutcome Verify(PasswordHash hash, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(hash);

        if (string.IsNullOrEmpty(providedPassword))
        {
            return PasswordVerificationOutcome.Failed;
        }

        var result = _hasher.VerifyHashedPassword(_hashOwner, hash.Value, providedPassword);

        return result switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Succeeded,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SucceededNeedsRehash,
            _ => PasswordVerificationOutcome.Failed
        };
    }
}
