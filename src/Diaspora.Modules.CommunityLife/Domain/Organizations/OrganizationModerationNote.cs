using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// A moderator's note on an organization.
/// <para>
/// The editorial trail. Never rendered to the organization's members and never published on the
/// bus — a note saying "second complaint about the same collection, watching" is a working record
/// between moderators, and broadcasting it would make every moderator write less honestly.
/// </para>
/// </summary>
public sealed class OrganizationModerationNote : Entity<Guid>
{
    private OrganizationModerationNote(
        Guid id, UserId authorId, string note, DateTimeOffset createdAt) : base(id)
    {
        AuthorId = authorId;
        Note = note;
        CreatedAt = createdAt;
    }

    private OrganizationModerationNote() { }

    public UserId AuthorId { get; private set; } = default!;

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static OrganizationModerationNote Create(
        Guid id, UserId authorId, string? note, DateTimeOffset createdAt)
    {
        var trimmed = note?.Trim() ?? string.Empty;

        if (trimmed.Length > CommunityLifeLimits.MaxModerationNote)
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxModerationNote];
        }

        return new OrganizationModerationNote(id, authorId, trimmed, createdAt);
    }
}
