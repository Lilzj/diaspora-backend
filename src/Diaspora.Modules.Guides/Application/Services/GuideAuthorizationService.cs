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
/// and each needs the same check: is there a caller, is the guide theirs, or are they staff.
/// Written dozens of times it would be written most times correctly, and the exception would
/// be a reader who can publish somebody else's guide.
/// </para>
/// <para>
/// <b>Staff bypass is deliberate and narrow.</b> A moderator or an administrator may act on any
/// guide, because moderation would otherwise require co-authorship of everything they touch.
/// It is checked through the kernel's <c>ICurrentUser.Role</c>, not through a permission,
/// because the permission catalogue already gates the endpoint and this is the second
/// question: not "may you moderate" but "is this guide yours".
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

    /// <summary>Whether the caller holds a platform staff role.</summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(GuideErrors.NotAuthenticated);

    /// <summary>
    /// Confirms the caller is the guide's author, or staff.
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

        if (IsStaff || guide.AuthorId == caller.Value)
        {
            return caller;
        }

        return Result.Failure<UserId>(GuideErrors.NotAuthor);
    }

    /// <summary>
    /// Whether the caller may read this guide at all.
    /// <para>
    /// The author and staff see everything of theirs, including drafts and hidden guides —
    /// an author locked out of their own moderated guide cannot fix it. Everyone else sees
    /// published, unhidden guides: public and unlisted alike, because unlisted means
    /// "reachable by link", and the caller evidently has the link.
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
