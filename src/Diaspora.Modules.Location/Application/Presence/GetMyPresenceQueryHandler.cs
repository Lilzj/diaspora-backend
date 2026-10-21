using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Presence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Presence;

/// <summary>The caller's presence setting. Off, with nothing remembered, until they switch it on.</summary>
public sealed class GetMyPresenceQueryHandler : IQueryHandler<GetMyPresenceQuery, PresenceResponse>
{
    private readonly IMemberPresenceRepository _presence;
    private readonly PresenceAreas _areas;
    private readonly ICurrentUser _currentUser;

    public GetMyPresenceQueryHandler(
        IMemberPresenceRepository presence, PresenceAreas areas, ICurrentUser currentUser)
    {
        _presence = presence;
        _areas = areas;
        _currentUser = currentUser;
    }

    public async Task<Result<PresenceResponse>> Handle(
        GetMyPresenceQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Result.Failure<PresenceResponse>(LocationErrors.NotAuthenticated);
        }

        var presence = await _presence.FindAsync(userId, cancellationToken);

        return Result.Success(await PresenceMapper.ToResponseAsync(presence, _areas, cancellationToken));
    }
}

/// <summary>The setting as the member reads it back.</summary>
internal static class PresenceMapper
{
    public static async Task<PresenceResponse> ToResponseAsync(
        MemberPresence? presence, PresenceAreas areas, CancellationToken cancellationToken)
    {
        var area = presence is { IsEnabled: true }
            ? (await areas.ChainAsync(presence.LastAreaId, cancellationToken)).Area
            : null;

        return new PresenceResponse
        {
            Enabled = presence?.IsEnabled == true,
            EnabledAt = presence?.EnabledAt,
            LastArea = PresenceAreas.Describe(area),
            LastEvaluatedAt = presence?.LastEvaluatedAt,
            MinimumIntervalSeconds = (int)PresenceAreas.MinimumInterval.TotalSeconds
        };
    }
}
