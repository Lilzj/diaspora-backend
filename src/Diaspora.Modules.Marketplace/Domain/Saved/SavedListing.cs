using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Saved;

/// <summary>
/// A listing a member is watching.
/// <para>
/// <b>Its own aggregate rather than a collection on either side, and both alternatives are worse.</b>
/// On the member it would be an unbounded list loaded with the profile; on the listing it would be
/// a popular item loading ten thousand rows to render a card. Keyed by (member, listing), which is
/// what makes saving idempotent in the database rather than by convention.
/// </para>
/// <para>
/// <b>Not published as an integration event.</b> Who is watching what is the most behaviourally
/// revealing thing this module knows about a member; no other module needs it, and putting it on
/// the bus would be a privacy cost paid for nothing.
/// </para>
/// </summary>
public sealed class SavedListing : AggregateRoot<SavedListingId>, IAuditableEntity
{
    private SavedListing(
        SavedListingId id,
        UserId userId,
        MarketplaceListingId listingId,
        string? note,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        ListingId = listingId;
        Note = note;
        CreatedAt = createdAt;
    }

    private SavedListing() { }

    public UserId UserId { get; private set; } = default!;

    public MarketplaceListingId ListingId { get; private set; } = default!;

    /// <summary>The member's own private note — "check the charger", "ask about delivery".</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Whether the member wants telling when the price moves. The one saved-listing feature that
    /// costs the platform anything, and the one buyers ask for most.
    /// </summary>
    public bool NotifyOnPriceChange { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<SavedListing> Create(
        SavedListingId id,
        UserId userId,
        MarketplaceListingId listingId,
        DateTimeOffset now,
        string? note = null,
        bool notifyOnPriceChange = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(listingId);

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxNoteLength })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxNoteLength];
        }

        var saved = new SavedListing(id, userId, listingId, trimmed, now)
        {
            NotifyOnPriceChange = notifyOnPriceChange
        };

        saved.RaiseDomainEvent(new ListingSavedDomainEvent
        {
            ListingId = listingId,
            UserId = userId,
            IsSaved = true
        });

        return Result.Success(saved);
    }

    /// <summary>
    /// Updates the private note and the price-watch preference.
    /// <para>
    /// An over-long note is clamped rather than refused, exactly as it is on creation. It is the
    /// member's own reminder to themselves; refusing the save over its length would lose the
    /// preference change they actually came to make.
    /// </para>
    /// </summary>
    public Result Update(string? note, bool notifyOnPriceChange)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxNoteLength })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxNoteLength];
        }

        Note = trimmed;
        NotifyOnPriceChange = notifyOnPriceChange;

        return Result.Success();
    }

    /// <summary>
    /// Raises the unsave fact. Called before the row is removed, because a hard delete cannot
    /// raise an event and the listing's save count has to come down.
    /// </summary>
    public void NoteRemoved() =>
        RaiseDomainEvent(new ListingSavedDomainEvent
        {
            ListingId = ListingId,
            UserId = UserId,
            IsSaved = false
        });

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
