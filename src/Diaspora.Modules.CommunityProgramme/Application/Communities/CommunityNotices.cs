using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A community's notices — FRONTEND-PHASE13-GAP-016, 21 Oct 2026.
//
// **Who reads.** A public or unlisted community's notices are as public as the community. A private
// or invite-only community's are its current members' — a notice is said to the community, and
// somebody who left or was removed is no longer in it.
//
// **Who writes.** Publishing is `CanPublishNotice`: the owner and administrators. Revising or removing
// is the same capability, or the notice's own author while they still belong — so a moderator who
// published something wrong can take it down without asking, and an administrator can take down
// anybody's.
//
// **Not delivered.** Publishing writes the notice and nothing else: no push and no inbox entry. The
// Notifications module has no consumer for a community notice, and adding a contract nobody consumes
// would be a fact written and never read. The app reads notices when the community is opened.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A community's notices: pinned first, then newest first.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListCommunityNoticesQuery : IQuery<IReadOnlyList<CommunityNoticeResponse>>
{
    public required string IdOrSlug { get; init; }

    public bool IncludeExpired { get; init; }
}

public sealed class ListCommunityNoticesQueryHandler
    : IQueryHandler<ListCommunityNoticesQuery, IReadOnlyList<CommunityNoticeResponse>>
{
    /// <summary>A notice board, not an archive. The newest fifty is every notice anybody reads.</summary>
    private const int Limit = 50;

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityNoticeRepository _notices;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListCommunityNoticesQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityNoticeRepository notices,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _notices = notices;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<IReadOnlyList<CommunityNoticeResponse>>> Handle(
        ListCommunityNoticesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var readable = await CommunityNoticeAccess.ResolveReaderAsync(
            query.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<IReadOnlyList<CommunityNoticeResponse>>(CommunityErrors.NotFound);
        }

        var notices = await _notices.ListAsync(
            readable.Community.Id, query.IncludeExpired, DateTimeOffset.UtcNow, Limit, cancellationToken);

        var names = await CommunityNoticeAccess.PublisherNamesAsync(
            readable, notices, _memberships, _profiles, cancellationToken);

        return Result.Success<IReadOnlyList<CommunityNoticeResponse>>(
            [.. notices.Select(notice => CommunityNoticeAccess.ToResponse(notice, readable, names, _access))]);
    }
}

/// <summary>Publish a notice. The owner's or an administrator's.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record PublishCommunityNoticeCommand : ICommand<CommunityNoticeResponse>
{
    public required string IdOrSlug { get; init; }

    public required SaveCommunityNoticeRequest Request { get; init; }
}

public sealed class PublishCommunityNoticeCommandHandler
    : ICommandHandler<PublishCommunityNoticeCommand, CommunityNoticeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityNoticeRepository _notices;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public PublishCommunityNoticeCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityNoticeRepository notices,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _notices = notices;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityNoticeResponse>> Handle(
        PublishCommunityNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityNoticeResponse>(caller.Error);
        }

        var readable = await CommunityReadAccess.ResolveAsync(
            command.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<CommunityNoticeResponse>(CommunityErrors.NotFound);
        }

        // No staff bypass: speaking for a community is the community's own act.
        if (!CommunityCapabilitySet.For(readable.Community, readable.Membership).CanPublishNotice)
        {
            return Result.Failure<CommunityNoticeResponse>(CommunityGovernanceErrors.CannotPublishNotice);
        }

        var published = CommunityNotice.Publish(
            CommunityNoticeId.New(),
            readable.Community.Id,
            caller.Value,
            CommunityNoticeAccess.HighestRole(readable.Membership),
            command.Request.Title,
            command.Request.Body,
            command.Request.ExpiresAt,
            command.Request.IsPinned ?? false,
            DateTimeOffset.UtcNow);

        if (published.IsFailure)
        {
            return Result.Failure<CommunityNoticeResponse>(published.Error);
        }

        _notices.Add(published.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await CommunityNoticeAccess.PublisherNamesAsync(
            readable, [published.Value], _memberships, _profiles, cancellationToken);

        return Result.Success(
            CommunityNoticeAccess.ToResponse(published.Value, readable, names, _access));
    }
}

/// <summary>Revise a notice. Its author while they belong, or the owner or an administrator.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record UpdateCommunityNoticeCommand : ICommand<CommunityNoticeResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid NoticeId { get; init; }

    public required SaveCommunityNoticeRequest Request { get; init; }
}

public sealed class UpdateCommunityNoticeCommandHandler
    : ICommandHandler<UpdateCommunityNoticeCommand, CommunityNoticeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityNoticeRepository _notices;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCommunityNoticeCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityNoticeRepository notices,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _notices = notices;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityNoticeResponse>> Handle(
        UpdateCommunityNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var target = await CommunityNoticeAccess.RequireEditableAsync(
            command.IdOrSlug, command.NoticeId, _communities, _memberships, _notices, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<CommunityNoticeResponse>(target.Error);
        }

        var (readable, notice) = target.Value;

        var revised = notice.Revise(
            command.Request.Title,
            command.Request.Body,
            command.Request.ExpiresAt,
            command.Request.IsPinned ?? notice.IsPinned);

        if (revised.IsFailure)
        {
            return Result.Failure<CommunityNoticeResponse>(revised.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await CommunityNoticeAccess.PublisherNamesAsync(
            readable, [notice], _memberships, _profiles, cancellationToken);

        return Result.Success(CommunityNoticeAccess.ToResponse(notice, readable, names, _access));
    }
}

/// <summary>Remove a notice. Its author while they belong, or the owner or an administrator.</summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
public sealed record DeleteCommunityNoticeCommand : ICommand<CommunityNoticeResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid NoticeId { get; init; }
}

public sealed class DeleteCommunityNoticeCommandHandler
    : ICommandHandler<DeleteCommunityNoticeCommand, CommunityNoticeResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityNoticeRepository _notices;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCommunityNoticeCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityNoticeRepository notices,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _notices = notices;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityNoticeResponse>> Handle(
        DeleteCommunityNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var target = await CommunityNoticeAccess.RequireEditableAsync(
            command.IdOrSlug, command.NoticeId, _communities, _memberships, _notices, _access,
            cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<CommunityNoticeResponse>(target.Error);
        }

        var (readable, notice) = target.Value;

        // Answered as it was, so the client can drop exactly this row from its list.
        var removed = CommunityNoticeAccess.ToResponse(
            notice, readable, new Dictionary<Guid, MemberProfile>(), _access);

        _notices.Remove(notice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(removed with { CanEdit = false });
    }
}

internal static class CommunityNoticeAccess
{
    private static readonly CommunityMembershipRole[] RolesByRank =
    [
        CommunityMembershipRole.Owner,
        CommunityMembershipRole.Administrator,
        CommunityMembershipRole.Moderator,
        CommunityMembershipRole.Ambassador
    ];

    public static CommunityMembershipRole HighestRole(CommunityMembership? membership)
    {
        if (membership is null)
        {
            return CommunityMembershipRole.Member;
        }

        foreach (var role in RolesByRank)
        {
            if (membership.Holds(role))
            {
                return role;
            }
        }

        return CommunityMembershipRole.Member;
    }

    /// <summary>The community, if the caller may read its notices. See the file header.</summary>
    public static async Task<ReadableCommunity?> ResolveReaderAsync(
        string idOrSlug,
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        var readable = await CommunityReadAccess.ResolveAsync(
            idOrSlug, communities, memberships, access, cancellationToken);

        if (readable is null)
        {
            return null;
        }

        var isOpen = readable.Community.Visibility
            is CommunityVisibility.Public or CommunityVisibility.Unlisted;

        return isOpen || access.IsStaff || readable.Membership?.IsCurrentMember == true
            ? readable
            : null;
    }

    public static async Task<Result<(ReadableCommunity Readable, CommunityNotice Notice)>> RequireEditableAsync(
        string idOrSlug,
        Guid noticeId,
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityNoticeRepository notices,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        var caller = access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<(ReadableCommunity, CommunityNotice)>(caller.Error);
        }

        var readable = await ResolveReaderAsync(idOrSlug, communities, memberships, access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<(ReadableCommunity, CommunityNotice)>(CommunityErrors.NotFound);
        }

        var notice = await notices.GetAsync(
            readable.Community.Id, CommunityNoticeId.From(noticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure<(ReadableCommunity, CommunityNotice)>(
                CommunityGovernanceErrors.NoticeNotFound);
        }

        return CanEdit(notice, readable, caller.Value)
            ? Result.Success((readable, notice))
            : Result.Failure<(ReadableCommunity, CommunityNotice)>(
                CommunityGovernanceErrors.CannotChangeNotice);
    }

    public static bool CanEdit(CommunityNotice notice, ReadableCommunity readable, UserId? caller)
    {
        if (caller is null)
        {
            return false;
        }

        // Derived without the staff bypass, as publishing is.
        if (CommunityCapabilitySet.For(readable.Community, readable.Membership).CanPublishNotice)
        {
            return true;
        }

        return notice.PublishedBy == caller
            && readable.Membership?.IsCurrentMember == true
            && readable.Community.Status != CommunityStatus.Archived;
    }

    /// <summary>
    /// Publisher names the caller may see. A publisher whose membership is visible to the community
    /// only is named to its members alone — the same line the role-holder listing draws.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, MemberProfile>> PublisherNamesAsync(
        ReadableCommunity readable,
        IReadOnlyCollection<CommunityNotice> notices,
        ICommunityMembershipRepository memberships,
        IMemberProfileService profiles,
        CancellationToken cancellationToken)
    {
        var publishers = notices.Select(notice => notice.PublishedBy).Distinct().ToList();

        if (publishers.Count == 0)
        {
            return new Dictionary<Guid, MemberProfile>();
        }

        var callerIsMember = readable.Membership?.IsCurrentMember == true;
        var nameable = new List<UserId>(publishers.Count);

        foreach (var publisher in publishers)
        {
            var standing = await memberships.FindAsync(readable.Community.Id, publisher, cancellationToken);

            if (standing is { IsCurrentMember: true }
                && (standing.Visibility == CommunityMembershipVisibility.Public
                    || (callerIsMember && standing.Visibility == CommunityMembershipVisibility.CommunityOnly)))
            {
                nameable.Add(publisher);
            }
        }

        return nameable.Count == 0
            ? new Dictionary<Guid, MemberProfile>()
            : await profiles.ResolveAsync(nameable, cancellationToken);
    }

    public static CommunityNoticeResponse ToResponse(
        CommunityNotice notice,
        ReadableCommunity readable,
        IReadOnlyDictionary<Guid, MemberProfile> names,
        CommunityProgrammeAccessService access) => new()
    {
        Id = notice.Id.Value,
        CommunityId = notice.CommunityId.Value,
        Title = notice.Title,
        Body = notice.Body,
        PublishedAt = notice.PublishedAt,
        PublishedByRole = notice.PublishedByRole,
        PublishedByName = names.TryGetValue(notice.PublishedBy.Value, out var profile)
            ? profile.DisplayName
            : null,
        ExpiresAt = notice.ExpiresAt,
        IsPinned = notice.IsPinned,
        CanEdit = CanEdit(notice, readable, access.CallerId)
    };
}
