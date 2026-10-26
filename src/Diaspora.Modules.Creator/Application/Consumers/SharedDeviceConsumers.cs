using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Referrals;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Creator.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// THE SHARED-DEVICE SIGNAL (D-13, CS-D5)
//
// Identity says when a member registers a device other accounts already use, and how many; never
// who. Financial holds the member's points for review. Here the member's profile is flagged, and a
// referral they were invited by waits for review instead of qualifying (C3.3): a referrer and an
// invitee on one phone is the shape of a self-referral.
//
// The review is the Financial hold's. Creator has no queue of its own; when staff lift the hold from
// the review queue, the flag clears and the referral is checked again.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Flags the member's profile when Identity says their new device is shared.</summary>
public sealed class AccountDeviceSharedCreatorConsumer
    : IntegrationEventHandler<AccountDeviceSharedIntegrationEvent>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AccountDeviceSharedCreatorConsumer(
        ICreatorProfileRepository creators, IUnitOfWork unitOfWork, IClock clock)
    {
        _creators = creators;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        AccountDeviceSharedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Every member's profile is opened on registration, before any device is registered.
        var creator = await _creators.FindByUserAsync(integrationEvent.UserId, cancellationToken);

        if (creator is null || creator.IsSharedDeviceFlagged)
        {
            return;
        }

        creator.FlagSharedDevice(_clock.UtcNow);
        _creators.Update(creator);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Staff lifting a member's points hold is the review a flagged member waits for: the flag clears, and
/// a referral the member was invited by is checked again, so a contribution made under review counts.
/// </summary>
public sealed class RewardConversionHoldLiftedCreatorConsumer
    : IntegrationEventHandler<RewardConversionHoldLiftedIntegrationEvent>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly ReferralQualificationService _referrals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RewardConversionHoldLiftedCreatorConsumer(
        ICreatorProfileRepository creators,
        ReferralQualificationService referrals,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _creators = creators;
        _referrals = referrals;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        RewardConversionHoldLiftedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var creator = await _creators.FindByUserAsync(integrationEvent.MemberUserId, cancellationToken);

        if (creator is null || !creator.ClearSharedDeviceFlag(_clock.UtcNow))
        {
            return;
        }

        _creators.Update(creator);

        await _referrals.TryQualifyAsync(integrationEvent.MemberUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
