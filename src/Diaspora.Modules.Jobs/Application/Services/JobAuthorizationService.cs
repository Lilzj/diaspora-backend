using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Answers the second authorisation question: may this caller do this <em>here</em>?
/// <para>
/// <b>The permission catalogue answers the first — "may this person recruit at all" — and it
/// cannot answer this one.</b> A member may own one employer, recruit at a second and have
/// nothing to do with a third, and none of that fits on a token. Only the employer aggregate
/// knows, and every employer-scoped command in the module comes through here to ask it.
/// </para>
/// <para>
/// <b>One implementation, used by every write handler.</b> Dozens of commands mutate a posting,
/// an application, an interview or an offer, and each needs the same check. Written dozens of
/// times it would be written most times correctly, and the exception would be a recruiter acting
/// on another company's applicants.
/// </para>
/// <para>
/// <b>Staff bypass is deliberate and narrow.</b> A moderator or administrator may act on any
/// employer, because moderation would otherwise require membership of everything they touch. It
/// is checked through the kernel's <c>ICurrentUser.Role</c> rather than through a permission,
/// because the permission catalogue already gated the endpoint and this is the second question:
/// not "may you moderate" but "is this yours".
/// </para>
/// </summary>
public sealed class JobAuthorizationService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;
    private readonly IEmployerProfileRepository _employers;
    private readonly ICandidateProfileRepository _candidates;

    public JobAuthorizationService(
        ICurrentUser currentUser,
        IEmployerProfileRepository employers,
        ICandidateProfileRepository candidates)
    {
        _currentUser = currentUser;
        _employers = employers;
        _candidates = candidates;
    }

    /// <summary>The caller, or null when the request is anonymous.</summary>
    public UserId? CallerId => _currentUser.UserId;

    /// <summary>Whether the caller holds a platform staff role.</summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(JobErrors.NotAuthenticated);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Employer scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loads an employer and confirms the caller holds at least the role asked for.
    /// <para>
    /// Returns the employer on success, so a handler that needs both — which is nearly all of
    /// them — makes one call rather than three. A missing employer and an employer the caller is
    /// not a member of are deliberately different answers: the first is a not-found, and the
    /// second is a forbidden the caller can act on by asking to be added.
    /// </para>
    /// </summary>
    public async Task<Result<EmployerProfile>> RequireEmployerAsync(
        EmployerProfileId employerId,
        EmployerMemberRole minimumRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerId);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<EmployerProfile>(caller.Error);
        }

        var employer = await _employers.GetAsync(employerId, cancellationToken);

        if (employer is null)
        {
            return Result.Failure<EmployerProfile>(JobErrors.EmployerNotFound);
        }

        if (IsStaff)
        {
            return Result.Success(employer);
        }

        if (!employer.HasAtLeast(caller.Value, minimumRole))
        {
            // Owner is asked for by exactly the operations only an owner may do — transferring,
            // closing, changing membership — so naming it specifically tells the caller what
            // they would need rather than leaving them to guess.
            return Result.Failure<EmployerProfile>(
                minimumRole == EmployerMemberRole.Owner
                    ? JobErrors.EmployerNotOwned
                    : employer.RoleOf(caller.Value) is null
                        ? JobErrors.NotEmployer
                        : JobErrors.NotRecruiter);
        }

        return Result.Success(employer);
    }

    /// <summary>
    /// Confirms the caller may act on a posting, and that its employer may still recruit.
    /// <para>
    /// The suspension check is here rather than in the aggregate because it spans two of them: a
    /// suspended employer's postings stay valid objects, but nobody may work them. Putting the
    /// check at every call site would mean forgetting it at one.
    /// </para>
    /// </summary>
    public async Task<Result<EmployerProfile>> RequirePostingAccessAsync(
        JobPosting posting,
        EmployerMemberRole minimumRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);

        var employer = await RequireEmployerAsync(
            posting.EmployerId, minimumRole, cancellationToken);

        if (employer.IsFailure)
        {
            return employer;
        }

        // Staff work the moderation queue on suspended employers by definition.
        if (!IsStaff && !employer.Value.CanRecruit)
        {
            return Result.Failure<EmployerProfile>(JobErrors.EmployerSuspended);
        }

        return employer;
    }

    /// <summary>
    /// Whether the caller may see a posting at all.
    /// <para>
    /// The owning employer's team and staff see everything of theirs, drafts and hidden postings
    /// included — a recruiter locked out of their own moderated advert cannot fix it. Everyone
    /// else sees publicly visible, unhidden postings: public and unlisted alike, because unlisted
    /// means "reachable by link" and the caller evidently has the link.
    /// </para>
    /// </summary>
    public async Task<bool> CanSeeAsync(
        JobPosting posting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);

        if (IsStaff)
        {
            return true;
        }

        if (posting.IsDeleted)
        {
            return false;
        }

        var isTeam = await IsMemberOfAsync(posting.EmployerId, cancellationToken);

        if (isTeam)
        {
            return true;
        }

        return posting.ModerationState != JobModerationState.Hidden
            && JobStatusTransitions.IsPubliclyVisible(posting.Status)
            && posting.Visibility is JobVisibility.Public or JobVisibility.Unlisted;
    }

    /// <summary>Whether the caller belongs to an employer in any role.</summary>
    public async Task<bool> IsMemberOfAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return false;
        }

        var employer = await _employers.GetAsync(employerId, cancellationToken);

        return employer?.RoleOf(userId) is not null;
    }

    /// <summary>
    /// The employer the caller is acting as, when they belong to exactly one.
    /// <para>
    /// A convenience for the dashboard routes that take no employer in the path. Null when the
    /// caller belongs to none — or to several, in which case the request has to name one rather
    /// than have the platform guess which company they meant.
    /// </para>
    /// </summary>
    public async Task<EmployerProfile?> ResolveSoleEmployerAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return null;
        }

        var employers = await _employers.GetForMemberAsync(userId, cancellationToken);

        return employers.Count == 1 ? employers[0] : null;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Candidate scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The caller's own candidate profile, or a failure telling them to create one.
    /// <para>
    /// A candidate command arrives with a member identity, not a profile identity, and resolving
    /// one to the other in a single place keeps "you need a profile first" a single error rather
    /// than a check each handler remembers differently.
    /// </para>
    /// </summary>
    public async Task<Result<CandidateProfile>> RequireCandidateAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CandidateProfile>(caller.Error);
        }

        var profile = await _candidates.GetByUserAsync(caller.Value, cancellationToken);

        return profile is null
            ? Result.Failure<CandidateProfile>(JobErrors.NotCandidate)
            : Result.Success(profile);
    }

    /// <summary>
    /// Confirms the caller owns something, by the member identifier stored on it.
    /// <para>
    /// Staff are deliberately <b>not</b> given a bypass here. Employer-scoped moderation is a
    /// legitimate staff activity; reading or altering a named person's own application, résumé or
    /// saved list is a different thing, and it should require a deliberate support tool rather
    /// than fall out of a role check in a general-purpose helper.
    /// </para>
    /// </summary>
    public Result<UserId> RequireOwner(UserId ownerId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        return caller.Value == ownerId
            ? caller
            : Result.Failure<UserId>(JobErrors.NotApplicant);
    }
}
