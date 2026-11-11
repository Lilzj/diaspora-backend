using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A member's community preferences — FRONTEND-PHASE13-GAP-012 and -021, 21 Oct 2026.
//
// The caller's own, always: there is no user id in either route. Reading before the member ever saved
// answers the privacy-safe defaults with `isSaved: false`, never a 404 — a settings screen that
// errored for everybody who had not yet opened it would be the screen nobody can open.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The caller's community preferences, or the defaults.</summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
public sealed record GetMyCommunityPreferencesQuery : IQuery<CommunityPreferencesResponse>;

public sealed class GetMyCommunityPreferencesQueryHandler
    : IQueryHandler<GetMyCommunityPreferencesQuery, CommunityPreferencesResponse>
{
    private readonly ICommunityMemberPreferencesRepository _preferences;
    private readonly CommunityProgrammeAccessService _access;

    public GetMyCommunityPreferencesQueryHandler(
        ICommunityMemberPreferencesRepository preferences, CommunityProgrammeAccessService access)
    {
        _preferences = preferences;
        _access = access;
    }

    public async Task<Result<CommunityPreferencesResponse>> Handle(
        GetMyCommunityPreferencesQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityPreferencesResponse>(caller.Error);
        }

        var stored = await _preferences.FindAsync(caller.Value, cancellationToken);

        return Result.Success(CommunityPreferencesMapper.ToResponse(
            stored ?? CommunityMemberPreferences.CreateFor(caller.Value), isSaved: stored is not null));
    }
}

/// <summary>Save the caller's community preferences. Answers what was stored.</summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
public sealed record SaveMyCommunityPreferencesCommand : ICommand<CommunityPreferencesResponse>
{
    public required SaveCommunityPreferencesRequest Request { get; init; }
}

public sealed class SaveMyCommunityPreferencesCommandHandler
    : ICommandHandler<SaveMyCommunityPreferencesCommand, CommunityPreferencesResponse>
{
    private readonly ICommunityMemberPreferencesRepository _preferences;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public SaveMyCommunityPreferencesCommandHandler(
        ICommunityMemberPreferencesRepository preferences,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _preferences = preferences;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityPreferencesResponse>> Handle(
        SaveMyCommunityPreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityPreferencesResponse>(caller.Error);
        }

        var request = command.Request;
        var stored = await _preferences.FindAsync(caller.Value, cancellationToken);
        var preferences = stored ?? CommunityMemberPreferences.CreateFor(caller.Value);

        var visibility = preferences.DefaultMembershipVisibility;

        if (!string.IsNullOrWhiteSpace(request.DefaultMembershipVisibility)
            && (!Enum.TryParse(request.DefaultMembershipVisibility.Trim(), ignoreCase: true, out visibility)
                || !Enum.IsDefined(visibility)))
        {
            return Result.Failure<CommunityPreferencesResponse>(CommunityErrors.UnknownMembershipVisibility);
        }

        var notifications = request.Notifications is null
            ? preferences.Notifications
            : [.. request.Notifications.Select(row => new CommunityNotificationPreference(
                row.CategoryKey ?? string.Empty,
                row.Scope?.Trim() ?? string.Empty,
                row.CommunityIds ?? []))];

        var replaced = preferences.Replace(
            request.CrossCommunityDiscoveryEnabled ?? preferences.CrossCommunityDiscoveryEnabled,
            request.ShowPrimaryCommunityOnProfile ?? preferences.ShowPrimaryCommunityOnProfile,
            request.ShowInterestedCommunitiesOnProfile ?? preferences.ShowInterestedCommunitiesOnProfile,
            request.ShowCommunityRolesOnProfile ?? preferences.ShowCommunityRolesOnProfile,
            visibility,
            request.PreferredContentLanguage,
            notifications);

        if (replaced.IsFailure)
        {
            return Result.Failure<CommunityPreferencesResponse>(replaced.Error);
        }

        if (stored is null)
        {
            _preferences.Add(preferences);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityPreferencesMapper.ToResponse(preferences, isSaved: true));
    }
}

internal static class CommunityPreferencesMapper
{
    public static CommunityPreferencesResponse ToResponse(
        CommunityMemberPreferences preferences, bool isSaved) => new()
    {
        CrossCommunityDiscoveryEnabled = preferences.CrossCommunityDiscoveryEnabled,
        ShowPrimaryCommunityOnProfile = preferences.ShowPrimaryCommunityOnProfile,
        ShowInterestedCommunitiesOnProfile = preferences.ShowInterestedCommunitiesOnProfile,
        ShowCommunityRolesOnProfile = preferences.ShowCommunityRolesOnProfile,
        PreferredContentLanguage = preferences.PreferredContentLanguage,
        DefaultMembershipVisibility = preferences.DefaultMembershipVisibility.ToString(),
        Notifications =
        [
            .. preferences.Notifications.Select(row => new CommunityNotificationPreferenceResponse
            {
                CategoryKey = row.CategoryKey,
                Scope = row.Scope,
                CommunityIds = row.CommunityIds
            })
        ],
        IsSaved = isSaved
    };
}
