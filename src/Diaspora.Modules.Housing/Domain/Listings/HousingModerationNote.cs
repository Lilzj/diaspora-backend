using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// One entry in a listing's editorial audit trail.
/// <para>
/// <b>Append-only, never edited and never pruned.</b> The trail is what a second moderator reads
/// before overturning a first one's decision, and what an appeal is answered from. A note that
/// could be edited would be a record of what somebody currently wishes they had written.
/// </para>
/// <para>
/// Adding one is deliberately not a content edit: it raises no update event and does not bump the
/// listing's revision, so annotating a listing cannot make the landlord's in-flight save fail.
/// </para>
/// </summary>
public sealed class HousingModerationNote
{
    private HousingModerationNote(
        Guid noteId, UserId authorId, string note, DateTimeOffset createdAt)
    {
        NoteId = noteId;
        AuthorId = authorId;
        Note = note;
        CreatedAt = createdAt;
    }

    private HousingModerationNote() { }

    public Guid NoteId { get; private set; }

    /// <summary>The moderator who wrote it.</summary>
    public UserId AuthorId { get; private set; } = default!;

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static HousingModerationNote Create(
        Guid noteId, UserId authorId, string? note, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        var trimmed = note?.Trim() ?? string.Empty;

        if (trimmed.Length > HousingLimits.MaxModerationNote)
        {
            trimmed = trimmed[..HousingLimits.MaxModerationNote];
        }

        return new HousingModerationNote(noteId, authorId, trimmed, createdAt);
    }
}
