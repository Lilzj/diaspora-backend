using Diaspora.Domain.Identity.ValueObjects;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Produces one-time codes and the hashes they are stored as.
/// </summary>
public interface IOtpCodeGenerator
{
    /// <summary>Generates a fresh code using a cryptographic RNG.</summary>
    string Generate();

    /// <summary>Hashes a code for storage or comparison. Deterministic.</summary>
    SecretHash ComputeHash(string code);
}
