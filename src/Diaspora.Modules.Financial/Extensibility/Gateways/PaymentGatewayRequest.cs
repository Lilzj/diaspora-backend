using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// What an adapter is told, in provider-independent terms.
/// <para>
/// <b>No card number, no token, no instrument of any kind appears here.</b> An adapter collects
/// those directly from the client through the provider's own SDK; the platform never sees them and
/// therefore never stores, logs or accidentally serialises them. <see cref="ClientContext"/> is an
/// opaque string the adapter defined and the platform passes through unread.
/// </para>
/// </summary>
public sealed record PaymentGatewayRequest
{
    public required Guid PaymentIntentId { get; init; }

    public required Money Amount { get; init; }

    public required PaymentMethodKind MethodKind { get; init; }

    public required string Description { get; init; }

    /// <summary>Opaque to the platform. Whatever the adapter told the client to send back.</summary>
    public string? ClientContext { get; init; }

    /// <summary>The provider's own reference, on a capture or a refund of an earlier operation.</summary>
    public string? ExternalReference { get; init; }

    public string? CountryCode { get; init; }

    public Guid? PayerUserId { get; init; }

    /// <summary>
    /// The financial account the payment is opened against.
    /// <para>
    /// Most adapters ignore this: an external provider is told an amount and a reference, not which
    /// of this platform's accounts is behind it. It is here for the one adapter whose funds are
    /// <em>inside</em> the platform — paying from a wallet balance needs to know whose balance — and
    /// it is the account on the intent rather than anything a request body named.
    /// </para>
    /// </summary>
    public Guid? AccountId { get; init; }

    /// <summary>Passed to the provider so a replay does not charge twice on their side either.</summary>
    public string? IdempotencyKey { get; init; }
}
