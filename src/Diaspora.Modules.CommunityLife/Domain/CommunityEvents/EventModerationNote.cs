using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// A moderator's note on an event. The editorial trail, never rendered publicly and never published.
/// </summary>
public sealed class EventModerationNote : Entity<Guid>
{
    private EventModerationNote(
        Guid id, UserId authorId, string note, DateTimeOffset createdAt) : base(id)
    {
        AuthorId = authorId;
        Note = note;
        CreatedAt = createdAt;
    }

    private EventModerationNote() { }

    public UserId AuthorId { get; private set; } = default!;

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static EventModerationNote Create(
        Guid id, UserId authorId, string? note, DateTimeOffset createdAt)
    {
        var trimmed = note?.Trim() ?? string.Empty;

        if (trimmed.Length > CommunityLifeLimits.MaxModerationNote)
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxModerationNote];
        }

        return new EventModerationNote(id, authorId, trimmed, createdAt);
    }
}
