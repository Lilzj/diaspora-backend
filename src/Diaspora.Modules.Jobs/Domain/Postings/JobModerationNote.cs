using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// One entry in a posting's editorial audit trail.
/// <para>
/// Append-only by construction — no mutator, no removal path — because the trail's value is
/// that it cannot be edited after the fact. A moderator who hides a fraudulent posting, a staff
/// member who reinstates one, and a colleague reviewing either decision six months later are
/// all served by the same unerasable record. Never rendered to candidates or employers.
/// </para>
/// </summary>
public sealed class JobModerationNote
{
    private JobModerationNote(Guid noteId, UserId authorId, string note, DateTimeOffset createdAt)
    {
        NoteId = noteId;
        AuthorId = authorId;
        Note = note;
        CreatedAt = createdAt;
    }

    private JobModerationNote() { }

    public Guid NoteId { get; private set; }

    /// <summary>The staff member who wrote it. A value, not a foreign key.</summary>
    public UserId AuthorId { get; private set; } = default!;

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    internal static JobModerationNote Create(
        Guid noteId, UserId authorId, string note, DateTimeOffset createdAt) =>
        new(noteId, authorId, note, createdAt);
}
