using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// A statement the people who run a community publish to it — FRONTEND-PHASE13-GAP-016, 21 Oct 2026.
/// <para>
/// <b>Not an organization announcement.</b> <see cref="Diaspora.Modules.CommunityProgramme.Domain.Announcements.CommunityAnnouncement"/> is an
/// organization speaking to its members or to a country; this is a community — the group people
/// join from the directory — speaking to itself. Different author, different audience, different
/// capability (<see cref="CommunityCapabilitySet.CanPublishNotice"/>), and keeping them apart is
/// what stops one being reachable through the other's permission.
/// </para>
/// <para>
/// <b>The publisher's role is recorded at the moment of publishing.</b> The client prints "Posted by
/// a Moderator" beside it, and that is a statement about who they were when they said it — a
/// moderator who later stepped down did not retroactively post as a member.
/// </para>
/// </summary>
public sealed class CommunityNotice : AggregateRoot<CommunityNoticeId>, IAuditableEntity
{
    public const int TitleMaximumLength = 140;
    public const int BodyMaximumLength = 4000;

    private CommunityNotice(
        CommunityNoticeId id,
        CommunityId communityId,
        UserId publishedBy,
        string publishedByRole,
        DateTimeOffset publishedAt)
        : base(id)
    {
        CommunityId = communityId;
        PublishedBy = publishedBy;
        PublishedByRole = publishedByRole;
        PublishedAt = publishedAt;
    }

    // EF Core.
    private CommunityNotice() { }

    public CommunityId CommunityId { get; private set; } = null!;

    public UserId PublishedBy { get; private set; } = null!;

    /// <summary>The publisher's highest role when they published. Attribution, not authority.</summary>
    public string PublishedByRole { get; private set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsPinned { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsLive(DateTimeOffset now) => ExpiresAt is null || ExpiresAt > now;

    public static Result<CommunityNotice> Publish(
        CommunityNoticeId id,
        CommunityId communityId,
        UserId publishedBy,
        CommunityMembershipRole publishedByRole,
        string? title,
        string? body,
        DateTimeOffset? expiresAt,
        bool isPinned,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(communityId);
        ArgumentNullException.ThrowIfNull(publishedBy);

        var notice = new CommunityNotice(id, communityId, publishedBy, publishedByRole.ToString(), now);
        var revised = notice.Revise(title, body, expiresAt, isPinned);

        return revised.IsFailure
            ? Result.Failure<CommunityNotice>(revised.Error)
            : Result.Success(notice);
    }

    /// <summary>Replaces what the notice says, when it lapses and whether it leads the list.</summary>
    public Result Revise(string? title, string? body, DateTimeOffset? expiresAt, bool isPinned)
    {
        var trimmedTitle = title?.Trim();
        var trimmedBody = body?.Trim();

        if (string.IsNullOrEmpty(trimmedTitle))
        {
            return Result.Failure(CommunityGovernanceErrors.NoticeTitleRequired);
        }

        if (trimmedTitle.Length > TitleMaximumLength)
        {
            return Result.Failure(CommunityGovernanceErrors.NoticeTitleLength);
        }

        if (string.IsNullOrEmpty(trimmedBody))
        {
            return Result.Failure(CommunityGovernanceErrors.NoticeBodyRequired);
        }

        if (trimmedBody.Length > BodyMaximumLength)
        {
            return Result.Failure(CommunityGovernanceErrors.NoticeBodyLength);
        }

        // Against the moment it was published, not now: a notice that has already lapsed may still
        // be corrected without the edit being refused for a date nobody is changing.
        if (expiresAt is { } expiry && expiry <= PublishedAt)
        {
            return Result.Failure(CommunityGovernanceErrors.NoticeExpiryInPast);
        }

        Title = trimmedTitle;
        Body = trimmedBody;
        ExpiresAt = expiresAt;
        IsPinned = isPinned;

        return Result.Success();
    }

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
