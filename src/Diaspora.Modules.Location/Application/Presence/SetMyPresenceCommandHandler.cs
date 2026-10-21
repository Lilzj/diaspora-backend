using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Presence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Presence;

/// <summary>
/// Switches "when the app is open" on or off. Off forgets the last area as well, so nothing about
/// where the member was outlives their saying no.
/// </summary>
public sealed class SetMyPresenceCommandHandler : ICommandHandler<SetMyPresenceCommand, PresenceResponse>
{
    private readonly IMemberPresenceRepository _presence;
    private readonly PresenceAreas _areas;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetMyPresenceCommandHandler(
        IMemberPresenceRepository presence,
        PresenceAreas areas,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _presence = presence;
        _areas = areas;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PresenceResponse>> Handle(
        SetMyPresenceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentUser.UserId is not { } userId)
        {
            return Result.Failure<PresenceResponse>(LocationErrors.NotAuthenticated);
        }

        var now = _clock.UtcNow;
        var presence = await _presence.FindAsync(userId, cancellationToken);

        if (presence is null)
        {
            if (!request.Enabled)
            {
                // Off, and nothing was ever stored. Writing a row to record a "no" would be keeping
                // something about a member who said no.
                return Result.Success(
                    await PresenceMapper.ToResponseAsync(null, _areas, cancellationToken));
            }

            presence = MemberPresence.For(userId, now);
            _presence.Add(presence);
        }

        presence.SetEnabled(request.Enabled, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await PresenceMapper.ToResponseAsync(presence, _areas, cancellationToken));
    }
}
