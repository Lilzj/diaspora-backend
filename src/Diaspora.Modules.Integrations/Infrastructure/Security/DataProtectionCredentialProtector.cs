using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Integrations.Infrastructure.Security;

/// <summary>
/// Seals provider tokens with ASP.NET Core Data Protection.
/// <para>
/// <b>Real encryption, not a placeholder.</b> Data Protection is the platform's existing key
/// infrastructure: it is already configured by the host, it already rotates keys on a schedule, and
/// it already supports a key ring backed by a certificate or a key vault in production. Writing a
/// bespoke AES wrapper here would have meant a second key lifecycle for one module to get wrong.
/// </para>
/// <para>
/// <b>The purpose string is the isolation boundary.</b> A payload sealed under this purpose cannot
/// be opened by a protector created for any other, so a bug elsewhere in the host cannot decrypt an
/// OAuth token by accident, and a token cannot be replayed into a different subsystem's unprotect
/// call. The purpose carries a version suffix so that changing the scheme is a new purpose rather
/// than a silent reinterpretation of old ciphertext.
/// </para>
/// <para>
/// <b><see cref="TryUnprotect"/> logs the failure without the ciphertext.</b> A value that will not
/// open is an operational event worth seeing — a restored backup, a key ring that was not persisted
/// — and logging the payload would put a token in a log the moment the key came back.
/// </para>
/// </summary>
public sealed class DataProtectionCredentialProtector : IIntegrationCredentialProtector
{
    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionCredentialProtector> _logger;

    public DataProtectionCredentialProtector(
        IDataProtectionProvider provider,
        IOptions<IntegrationsOptions> options,
        ILogger<DataProtectionCredentialProtector> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        var purpose = string.IsNullOrWhiteSpace(options.Value.CredentialProtectionPurpose)
            ? "Diaspora.Integrations.Credentials.v1"
            : options.Value.CredentialProtectionPurpose.Trim();

        KeyId = purpose;
        _protector = provider.CreateProtector(purpose);
        _logger = logger;
    }

    /// <summary>
    /// The purpose string, recorded on every connection.
    /// <para>
    /// Data Protection manages its own key versions inside the payload, so the identifier this
    /// module stores is the purpose rather than a key number — which is the thing that actually
    /// determines whether a stored value can be opened by the running process.
    /// </para>
    /// </summary>
    public string KeyId { get; }

    /// <summary>
    /// Always true.
    /// <para>
    /// Data Protection is registered by the host and has a key ring in every environment; there is
    /// no configuration a deployment can omit that would leave this unable to seal. The property
    /// exists on the interface because a key-vault-backed protector shipped later genuinely can be
    /// unconfigured, and every call site already branches on it.
    /// </para>
    /// </summary>
    public bool IsConfigured => true;

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

        return _protector.Protect(plaintext);
    }

    public bool TryUnprotect(string cipher, out string plaintext)
    {
        plaintext = string.Empty;

        if (string.IsNullOrWhiteSpace(cipher))
        {
            return false;
        }

        try
        {
            plaintext = _protector.Unprotect(cipher);

            return true;
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            // No ciphertext, no length, no prefix. The one fact worth recording is that a stored
            // credential could not be opened, which is enough to start an investigation and not
            // enough to help anybody who reads the log.
            _logger.LogWarning(
                exception,
                "A stored integration credential could not be opened under the current key ring. "
                + "The connection will be moved to reauthorization-required.");

            return false;
        }
    }
}
