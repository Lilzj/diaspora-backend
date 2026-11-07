using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Answers "may this caller do this to this guide?".
/// <para>
/// <b>One implementation, used by every write handler.</b> Dozens of commands mutate a guide,
/// and each needs the same check: is there a caller, and is the guide theirs. Written dozens of
/// times it would be written most times correctly, and the exception would be a reader who can
/// publish somebody else's guide.
/// </para>
/// <para>
/// <b>Staff never pass the author check.</b> A moderator or an administrator does not edit,
/// draft, publish, schedule, archive, restore, delete or otherwise change a member's guide
/// through the author's own routes: there are no support edits on a member's behalf. Staff act
/// on somebody else's guide only through the moderation routes — hide, unhide, moderation notes,
/// featuring and the queue — which are gated on their own permission and never come through
/// <see cref="RequireAuthor"/>; and Trust's decisions and Community's moderation hook apply to
/// the aggregate directly. What staff keep is <em>sight</em>: <see cref="CanSee"/> and the
/// staff audience on the query filters, because a moderator must see a hidden or unpublished
/// guide to review it.
/// </para>
/// </summary>
public sealed class GuideAuthorizationService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;

    public GuideAuthorizationService(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>The caller, or null when the request is anonymous.</summary>
    public UserId? CallerId => _currentUser.UserId;

    /// <summary>
    /// Whether the caller holds a platform staff role. Decides what a caller may <em>see</em>,
    /// never what they may change.
    /// </summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(GuideErrors.NotAuthenticated);

    /// <summary>
    /// Confirms the caller is the guide's author. Staff are refused like anybody else.
    /// <para>
    /// Returns the caller's identifier on success, so a handler that needs both — which is
    /// nearly all of them, for the event payload — makes one call rather than two.
    /// </para>
    /// </summary>
    public Result<UserId> RequireAuthor(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        if (guide.AuthorId == caller.Value)
        {
            return caller;
        }

        return Result.Failure<UserId>(GuideErrors.NotAuthor);
    }

    /// <summary>
    /// Confirms the caller may read what only the author reads about a guide — its analytics, its
    /// verification status: the author, or staff reviewing it.
    /// <para>
    /// <b>A read, and only a read.</b> Nothing that changes a guide may use this; a write goes
    /// through <see cref="RequireAuthor"/>, which staff never pass.
    /// </para>
    /// </summary>
    public Result<UserId> RequireAuthorOrStaffToRead(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        return IsStaff ? caller : RequireAuthor(guide);
    }

    /// <summary>
    /// Whether the caller may read this guide at all.
    /// <para>
    /// The author and staff see everything of theirs, including drafts and hidden guides —
    /// an author locked out of their own moderated guide cannot fix it, and a moderator who
    /// cannot see a hidden guide cannot review it. Everyone else sees published, unhidden
    /// guides: public and unlisted alike, because unlisted means "reachable by link", and the
    /// caller evidently has the link.
    /// </para>
    /// </summary>
    public bool CanSee(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        if (IsStaff)
        {
            return true;
        }

        var isAuthor = _currentUser.UserId is { } caller && guide.AuthorId == caller;

        if (guide.IsDeleted)
        {
            return false;
        }

        if (isAuthor)
        {
            return true;
        }

        // Published, Public or Unlisted, not hidden. One rule, on the aggregate, because the
        // contract that tells savers about an edit reads the same one.
        return guide.IsReadableByOthers;
    }
}
