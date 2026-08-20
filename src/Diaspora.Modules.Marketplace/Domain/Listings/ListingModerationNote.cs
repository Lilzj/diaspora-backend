using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// One entry in a listing's editorial audit trail.
/// <para>
/// <b>Append-only, never edited and never pruned.</b> The trail is what a second moderator reads
/// before overturning a first one's decision, and what an appeal is answered from. A note that
/// could be edited would be a record of what somebody currently wishes they had written.
/// </para>
/// <para>
/// Adding one is deliberately not a content edit: it raises no update event and does not bump the
/// listing's revision, so annotating a listing cannot make the seller's in-flight save fail.
/// </para>
/// </summary>
public sealed class ListingModerationNote
{
    private ListingModerationNote(
        Guid noteId, UserId authorId, string note, DateTimeOffset createdAt)
    {
        NoteId = noteId;
        AuthorId = authorId;
        Note = note;
        CreatedAt = createdAt;
    }

    private ListingModerationNote() { }

    public Guid NoteId { get; private set; }

    /// <summary>The moderator who wrote it.</summary>
    public UserId AuthorId { get; private set; } = default!;

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static ListingModerationNote Create(
        Guid noteId, UserId authorId, string note, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        var trimmed = note?.Trim() ?? string.Empty;

        if (trimmed.Length > MarketplaceLimits.MaxModerationNote)
        {
            trimmed = trimmed[..MarketplaceLimits.MaxModerationNote];
        }

        return new ListingModerationNote(noteId, authorId, trimmed, createdAt);
    }
}
