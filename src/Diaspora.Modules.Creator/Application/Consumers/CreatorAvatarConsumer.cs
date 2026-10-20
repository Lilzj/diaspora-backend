using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Creator.Application.Consumers;

/// <summary>
/// Keeps a creator's photo in step with the member's profile photo (20 Oct 2026).
/// <para>
/// <c>AvatarUrl</c> was only ever written from registration, which carries no photo, so every
/// creator card showed initials whatever the member had uploaded. Idempotent: a redelivery finds
/// the same address and changes nothing. Every member has a record from registration; one opened
/// later by a first read misses a photo set before it, until the member changes it again.
/// </para>
/// </summary>
public sealed class UserAvatarChangedCreatorConsumer
    : IntegrationEventHandler<UserAvatarChangedIntegrationEvent>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UserAvatarChangedCreatorConsumer(
        ICreatorProfileRepository creators, IUnitOfWork unitOfWork, IClock clock)
    {
        _creators = creators;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        UserAvatarChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var creator = await _creators.FindByUserAsync(integrationEvent.UserId, cancellationToken);

        if (creator is null)
        {
            return;
        }

        creator.ChangeAvatar(integrationEvent.AvatarUrl, _clock.UtcNow);

        _creators.Update(creator);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
