using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Domain.Policies;

/// <summary>
/// The legal moves through a negotiation.
/// <para>
/// <b>Pending and Countered are the same live state seen from opposite ends</b>, which is why
/// they can each become the other: a counter answers whatever was on the table and hands the turn
/// back. Everything else is an exit, and every exit is final — a negotiation that could be
/// reopened after acceptance would be one where a seller's committed stock could be taken back
/// from the buyer who won it.
/// </para>
/// </summary>
public static class OfferStatusTransitions
{
    private static readonly Dictionary<OfferStatus, OfferStatus[]> Allowed = new()
    {
        [OfferStatus.Pending] =
        [
            OfferStatus.Countered,
            OfferStatus.Accepted,
            OfferStatus.Rejected,
            OfferStatus.Withdrawn,
            OfferStatus.Expired
        ],

        [OfferStatus.Countered] =
        [
            OfferStatus.Countered,
            OfferStatus.Accepted,
            OfferStatus.Rejected,
            OfferStatus.Withdrawn,
            OfferStatus.Expired
        ],

        [OfferStatus.Accepted] = [],
        [OfferStatus.Rejected] = [],
        [OfferStatus.Withdrawn] = [],
        [OfferStatus.Expired] = []
    };

    /// <summary>
    /// Whether a negotiation may move from one status to another. Countered to Countered is
    /// legal and is the ordinary case: it is the next round.
    /// </summary>
    public static bool CanTransition(OfferStatus from, OfferStatus to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    public static IReadOnlyList<OfferStatus> From(OfferStatus status) =>
        Allowed.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>Whether the negotiation is still live.</summary>
    public static bool IsOpen(OfferStatus status) =>
        status is OfferStatus.Pending or OfferStatus.Countered;

    /// <summary>Whether the negotiation has ended, however it ended.</summary>
    public static bool IsSettled(OfferStatus status) => !IsOpen(status);
}
