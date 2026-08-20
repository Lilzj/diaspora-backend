using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// What a provider calls this payment on its side.
/// <para>
/// <b>The provider is a string, and it is the only place in this assembly a provider is named at
/// all.</b> It arrives from whichever <c>IPaymentGateway</c> is registered and is stored verbatim;
/// nothing in this module switches on it, and an architecture test asserts that no vendor name
/// appears in this assembly's types. That is what makes the platform provider-independent in fact
/// rather than in intention.
/// </para>
/// <para>
/// <see cref="ExternalReference"/> is the reconciliation key. Without it a captured payment cannot
/// be matched to a provider's settlement file, which is the one operation nobody thinks about until
/// the first month-end.
/// </para>
/// </summary>
public sealed class GatewayReference : ValueObject
{
    private GatewayReference(string provider, string externalReference, string? externalStatus)
    {
        Provider = provider;
        ExternalReference = externalReference;
        ExternalStatus = externalStatus;
    }

    private GatewayReference() { }

    /// <summary>Whatever the registered adapter calls itself. Opaque here.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>The provider's own identifier for this payment. The reconciliation key.</summary>
    public string ExternalReference { get; private set; } = string.Empty;

    /// <summary>The provider's own status string, kept unmapped for support and reconciliation.</summary>
    public string? ExternalStatus { get; private set; }

    public static GatewayReference Create(
        string provider, string externalReference, string? externalStatus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);

        return new GatewayReference(
            provider.Trim(),
            externalReference.Trim(),
            string.IsNullOrWhiteSpace(externalStatus) ? null : externalStatus.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Provider;
        yield return ExternalReference;
        yield return ExternalStatus;
    }
}
