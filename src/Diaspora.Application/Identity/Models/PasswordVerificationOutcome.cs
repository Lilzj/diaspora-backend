namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Result of checking a password against a stored hash.
/// </summary>
public enum PasswordVerificationOutcome
{
    Failed = 0,

    Succeeded = 1,

    /// <summary>
    /// Correct, but hashed with outdated parameters. The caller should re-hash and
    /// persist while it holds the plaintext.
    /// </summary>
    SucceededNeedsRehash = 2
}
