using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Consumers.Rewards;

/// <summary>
/// Holds a member's points for review when Identity says their new device is one other accounts
/// already use (D-13, CS-D5).
/// <para>
/// <b>A system hold, decided by nobody.</b> Points still accrue and vest; nothing converts, and no
/// converted credit is released, until staff lift it from the review queue
/// (<c>GET /operations/rewards/holds</c>). The note is an operator's and never reaches the member.
/// </para>
/// <para>
/// <b>A hold already in force is left alone.</b> A second announcement, a redelivery, or a hold staff
/// placed for their own reason keeps the note it has: the platform never writes over a person.
/// </para>
/// </summary>
public sealed class AccountDeviceSharedRewardHoldConsumer
    : IntegrationEventHandler<AccountDeviceSharedIntegrationEvent>
{
    public const string Reason = "Shares a device with another account; awaiting review";

    private readonly IRewardAccountRepository _accounts;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AccountDeviceSharedRewardHoldConsumer(
        IRewardAccountRepository accounts, IGuidGenerator guids, IClock clock, IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        AccountDeviceSharedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var account = await _accounts.FindByMemberAsync(integrationEvent.UserId, cancellationToken);

        if (account is { IsConversionBlocked: true })
        {
            return;
        }

        var now = _clock.UtcNow;

        // A member with no points yet is held all the same, as a staff hold opens the account: the
        // first points they earn must not convert before the review.
        if (account is null)
        {
            account = RewardAccount.Open(
                RewardAccountId.From(_guids.NewSequentialGuid()), integrationEvent.UserId, now);

            _accounts.Add(account);
        }

        account.BlockConversion(_guids.NewSequentialGuid(), Reason, decidedByUserId: null, now);
        _accounts.Update(account);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
