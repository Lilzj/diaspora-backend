using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Consumers.Rewards;

/// <summary>
/// Lifts a member's points hold when Trust approves their appeal against it (RWD-14, CS-4a).
/// <para>
/// <b>A held member asks through Trust's appeals</b>, where reviewers already have a queue and an
/// SLA. The approval is a staff decision like a lift from the review queue: it is recorded in the
/// reviewer's name, and announced the same way, so a shared-device flag clears in Creator too. A
/// rejected appeal changes nothing here; the appeal keeps the decision.
/// </para>
/// </summary>
public sealed class AppealApprovedRewardHoldConsumer
    : IntegrationEventHandler<AppealApprovedIntegrationEvent>
{
    public const string AppealType = "RewardHold";

    public const string Reason = "Appeal approved.";

    private readonly IRewardAccountRepository _accounts;
    private readonly FinancialIntegrationEventRelay _relay;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AppealApprovedRewardHoldConsumer(
        IRewardAccountRepository accounts,
        FinancialIntegrationEventRelay relay,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _relay = relay;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        AppealApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(integrationEvent.AppealType, AppealType, StringComparison.Ordinal))
        {
            return;
        }

        // The appellant's own account: the appeal's target is the appellant (Trust refuses any other).
        var account = await _accounts.FindByMemberAsync(integrationEvent.UserId, cancellationToken);
        var now = _clock.UtcNow;

        if (account is null
            || !account.AllowConversion(_guids.NewSequentialGuid(), Reason, integrationEvent.DecidedByUserId, now))
        {
            return;
        }

        _accounts.Update(account);

        // As a lift from the review queue is: Creator clears its shared-device flag on it.
        await _relay.PublishHoldLiftedAsync(
            integrationEvent.UserId, integrationEvent.DecidedByUserId, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
