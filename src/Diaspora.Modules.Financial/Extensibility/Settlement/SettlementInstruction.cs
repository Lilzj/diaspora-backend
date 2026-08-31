using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Settlement;

/// <summary>
/// One counterparty, one amount, and enough about the destination for a provider to act on it.
/// <para>
/// <b>The destination fields were added when the first real adapter proved they had to be.</b> The
/// instruction used to carry a Diaspora user identifier and nothing else, which is sufficient for a
/// settlement engine that already knows the platform's users and useless for every provider that
/// does not. A provider is asked to move money to <em>an account it issued</em> — a connected
/// account, a recipient record, a transfer token — and it identifies that account by its own
/// reference, never by ours.
/// </para>
/// <para>
/// <b>A reference belongs to the provider that issued it.</b> <see cref="DestinationProvider"/> says
/// which one, and an adapter must refuse an instruction naming a different provider rather than
/// passing an opaque string to an API that will interpret it as one of its own. The router keeps
/// this from arising — a destination bound to a provider is only ever routed to that provider — and
/// the check here is the second lock on the same door.
/// </para>
/// </summary>
public sealed record SettlementInstruction
{
    /// <summary>The platform's identifier for the payee. Meaningless to a provider.</summary>
    public required Guid CounterpartyId { get; init; }

    /// <summary>What the provider is asked to move. Net of the platform's fee.</summary>
    public required Money Amount { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    /// <summary>
    /// The platform's handle for this movement, carried so a provider can be asked about it later
    /// and a redelivered instruction is recognisable as the same one.
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// The provider's own identifier for where the money is going — the token it issued when the
    /// destination was registered. Null when the destination has none, which an adapter that needs
    /// one must treat as a refusal rather than a blank to fill in.
    /// </summary>
    public string? DestinationReference { get; init; }

    /// <summary>
    /// Which provider issued <see cref="DestinationReference"/>. An adapter must refuse an
    /// instruction naming a provider that is not itself - see the type summary.
    /// </summary>
    public string? DestinationProvider { get; init; }

    /// <summary>The rail. Some providers take a different API path per rail.</summary>
    public PayoutDestinationKind? DestinationKind { get; init; }

    /// <summary>ISO 3166-1 alpha-2, where the payee is. Not where the platform is.</summary>
    public string? DestinationCountry { get; init; }

    /// <summary>What the member sees. Masked at the source; never a credential.</summary>
    public string? DestinationLabel { get; init; }
}
