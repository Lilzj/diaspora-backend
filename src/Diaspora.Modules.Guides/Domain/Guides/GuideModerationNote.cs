using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Domain.Guides;

/// <summary>
/// One entry in the guide's editorial audit trail.
/// <para>
/// Append-only by construction — there is no mutator and no removal path — because the trail's
/// value is that it cannot be edited after the fact. A moderator who hides a guide, a staff
/// member who features one, and a colleague reviewing either decision six months later are all
/// served by the same unerasable record. Never rendered to readers.
/// </para>
/// </summary>
public sealed class GuideModerationNote
{
    private GuideModerationNote(Guid noteId, UserId authorId, string note, DateTimeOffset createdAt)
    {
        NoteId = noteId;
        AuthorId = authorId;
        Note = note;
        CreatedAt = createdAt;
    }

    private GuideModerationNote() { }

    public Guid NoteId { get; private set; }

    /// <summary>The staff member who wrote it. A value, not a foreign key.</summary>
    public UserId AuthorId { get; private set; } = default!;

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    internal static GuideModerationNote Create(
        Guid noteId, UserId authorId, string note, DateTimeOffset createdAt) =>
        new(noteId, authorId, note, createdAt);
}
