using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Offers;

/// <summary>
/// One move in a negotiation.
/// <para>
/// <b>This is the brief's "complete negotiation history", and it is append-only.</b> Every offer,
/// counter, acceptance, rejection, withdrawal and expiry lands here with its amount, its author
/// and its instant. Keeping only the current amount would make the aggregate cheaper and would
/// lose the single most useful thing about a disputed sale: what was actually said, in order, by
/// whom.
/// </para>
/// <para>
/// <b><see cref="ActorId"/> is nullable and <see cref="Party"/> is not.</b> An expiry is recorded
/// by the sweep and has no author, but it is still unambiguously the move that ended the buyer's
/// turn — so the side is always known even when the person is not.
/// </para>
/// </summary>
public sealed class NegotiationEntry
{
    private NegotiationEntry(
        Guid entryId,
        int round,
        NegotiationAction action,
        OfferParty party,
        UserId? actorId,
        decimal? amount,
        string currencyCode,
        int quantity,
        string? message,
        DateTimeOffset occurredAt)
    {
        EntryId = entryId;
        Round = round;
        Action = action;
        Party = party;
        ActorId = actorId;
        Amount = amount;
        CurrencyCode = currencyCode;
        Quantity = quantity;
        Message = message;
        OccurredAt = occurredAt;
    }

    private NegotiationEntry() { }

    public Guid EntryId { get; private set; }

    /// <summary>Its position in the sequence, from one. What the round ceiling counts.</summary>
    public int Round { get; private set; }

    public NegotiationAction Action { get; private set; }

    /// <summary>Which side moved. Always known, even when the actor is not.</summary>
    public OfferParty Party { get; private set; }

    /// <summary>Who moved. Null on an expiry, which the clock recorded.</summary>
    public UserId? ActorId { get; private set; }

    /// <summary>What was on the table after this move. Null on a rejection or a withdrawal.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>The listing's currency, carried on every entry so history reads standalone.</summary>
    public string CurrencyCode { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    /// <summary>What the party said. Plain text.</summary>
    public string? Message { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static NegotiationEntry Create(
        Guid entryId,
        int round,
        NegotiationAction action,
        OfferParty party,
        UserId? actorId,
        decimal? amount,
        string currencyCode,
        int quantity,
        string? message,
        DateTimeOffset occurredAt)
    {
        var trimmed = string.IsNullOrWhiteSpace(message) ? null : message.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxOfferMessage })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxOfferMessage];
        }

        return new NegotiationEntry(
            entryId, round, action, party, actorId, amount,
            currencyCode ?? string.Empty, quantity, trimmed, occurredAt);
    }
}
