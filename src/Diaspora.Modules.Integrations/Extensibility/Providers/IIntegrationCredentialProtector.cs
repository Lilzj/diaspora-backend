namespace Diaspora.Modules.Integrations.Extensibility.Providers;

/// <summary>
/// Seals and opens the tokens this module holds.
/// <para>
/// <b>The seam exists so that "encrypted at rest" is a property of the platform rather than of a
/// database's configuration.</b> A deployment on a managed database with transparent encryption has
/// tokens that are readable by anybody who can run a query, which is not what "encrypted at rest"
/// means to somebody reading the register's requirement.
/// </para>
/// <para>
/// <b><see cref="KeyId"/> is what makes rotation possible.</b> Every sealed value records the key
/// version that sealed it, so rotating is a re-seal of the rows naming the old version rather than a
/// re-authorization of every member on the platform. Without it, rotation and total credential loss
/// are the same operation.
/// </para>
/// <para>
/// <b><see cref="TryUnprotect"/> returns false rather than throwing.</b> A value sealed under a key
/// this process no longer has is an ordinary operational state — a restored backup, a rotated key
/// with too short a retirement window — and the correct handling is to move the connection to
/// <c>REAUTH_REQUIRED</c> and tell the member. An exception at that point would be a 500 on a
/// settings screen.
/// </para>
/// </summary>
public interface IIntegrationCredentialProtector
{
    /// <summary>The key version new values are sealed under. Recorded on every connection.</summary>
    string KeyId { get; }

    /// <summary>
    /// Whether the protector has a key.
    /// <para>
    /// False means this deployment cannot hold a credential, which means the OAuth exchange must
    /// refuse rather than store a token in the clear. That refusal is
    /// <c>ConfigurationMissing</c>, and it is the correct answer — an exchange that fell back to
    /// plaintext because a key was missing would be the single worst defect available to this
    /// module.
    /// </para>
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Seals a token. Returns ciphertext bound to <see cref="KeyId"/>.</summary>
    string Protect(string plaintext);

    /// <summary>Opens a sealed token. False when the key that sealed it is gone.</summary>
    bool TryUnprotect(string cipher, out string plaintext);
}
