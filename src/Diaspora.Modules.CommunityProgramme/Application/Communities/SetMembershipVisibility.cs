using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// Who may see that the caller belongs here — §41.
/// <para>
/// <b>The other half of the privacy default, and without it the default is a dead end.</b> A new
/// membership is Private so that joining publishes nothing; this is the only way a member can decide
/// otherwise. A field nobody can set is not a privacy model, it is a column.
/// </para>
/// <para>
/// <b>The member's own choice, and no administrative form of this command exists.</b> A community
/// cannot make its roll public on its members' behalf, because the disclosure is theirs — a diaspora
/// community, a faith community or a support group can reveal nationality, immigration status,
/// religion or health.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record SetMembershipVisibilityCommand : ICommand<CommunityMembershipResponse>
{
    public required string IdOrSlug { get; init; }

    /// <summary><c>Private</c>, <c>CommunityOnly</c> or <c>Public</c>.</summary>
    public string? Visibility { get; init; }
}

public sealed class SetMembershipVisibilityCommandHandler
    : ICommandHandler<SetMembershipVisibilityCommand, CommunityMembershipResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public SetMembershipVisibilityCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityMembershipResponse>> Handle(
        SetMembershipVisibilityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(caller.Error);
        }

        // Parsed before anything is read, and an unrecognised word is refused rather than
        // defaulted. Defaulting would be defensible in either direction and wrong in both: to
        // Private it silently ignores somebody opting in, to Public it discloses on a typo.
        if (!Enum.TryParse<CommunityMembershipVisibility>(
                command.Visibility?.Trim(), ignoreCase: true, out var visibility)
            || !Enum.IsDefined(visibility))
        {
            return Result.Failure<CommunityMembershipResponse>(
                CommunityErrors.UnknownMembershipVisibility);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityErrors.NotFound);
        }

        var membership = await _memberships.FindAsync(
            community.Id, caller.Value, cancellationToken);

        if (membership is null)
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityErrors.NotAMember);
        }

        membership.SetVisibility(visibility);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            CommunityMembershipMapper.ToResponse(community, membership, _access.IsStaff));
    }
}
