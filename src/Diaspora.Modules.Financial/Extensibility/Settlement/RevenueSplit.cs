using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Settlement;

/// <summary>
/// How one amount divides between the platform and a counterparty.
/// <para>
/// The shape a future split-payment or escrow engine needs, produced today by the commission rules
/// and consumed today only by the ledger. Nothing pays anybody out; the split is what the posting
/// uses to decide which wallets the legs touch.
/// </para>
/// </summary>
public sealed record RevenueSplit
{
    public required Money Gross { get; init; }

    /// <summary>What the platform keeps.</summary>
    public required Money PlatformShare { get; init; }

    /// <summary>What the counterparty is owed.</summary>
    public required Money CounterpartyShare { get; init; }

    /// <summary>Who the counterparty is. Null when the platform keeps everything.</summary>
    public Guid? CounterpartyId { get; init; }

    public string? PolicyVersion { get; init; }
}
