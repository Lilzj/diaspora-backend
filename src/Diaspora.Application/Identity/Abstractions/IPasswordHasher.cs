using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.ValueObjects;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Turns plaintext passwords into stored hashes and back-verifies them.
/// <para>
/// The algorithm lives entirely behind this port. Rehashing is surfaced explicitly so a
/// future iteration-count increase upgrades members' hashes as they sign in, rather than
/// requiring a migration nobody can run against one-way data.
/// </para>
/// </summary>
public interface IPasswordHasher
{
    PasswordHash Hash(string password);

    PasswordVerificationOutcome Verify(PasswordHash hash, string providedPassword);

    /// <summary>
    /// A hash of a fixed dummy value, for verifying against when no account matched.
    /// <para>
    /// Sign-in must cost the same whether or not the phone number is registered. Without
    /// this, an unregistered number returns in microseconds while a registered one pays
    /// for a deliberately slow hash, and the difference is a free account-enumeration
    /// oracle over the network.
    /// </para>
    /// </summary>
    PasswordHash DummyHash { get; }
}
