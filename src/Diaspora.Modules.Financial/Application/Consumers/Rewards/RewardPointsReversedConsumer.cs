using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Consumers.Rewards;

/// <summary>
/// Takes back pending points a grantor withdrew (D-9, RWD-08).
/// <para>
/// <b>Silent on everything but a reversal, like the grant consumer.</b> A grant that vested stays
/// earned and the request is recorded on it; a grant already decided, or one this account never
/// received, changes nothing. None of those is an error the bus should redeliver.
/// </para>
/// </summary>
public sealed class RewardPointsReversedConsumer
    : IntegrationEventHandler<RewardPointsReversedIntegrationEvent>
{
    private readonly IRewardAccountRepository _accounts;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RewardPointsReversedConsumer> _logger;

    public RewardPointsReversedConsumer(
        IRewardAccountRepository accounts,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<RewardPointsReversedConsumer> logger)
    {
        _accounts = accounts;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        RewardPointsReversedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (string.IsNullOrWhiteSpace(integrationEvent.GrantReference)
            || string.IsNullOrWhiteSpace(integrationEvent.Reason))
        {
            return;
        }

        var account = await _accounts.FindByMemberAsync(
            integrationEvent.MemberUserId, cancellationToken);

        if (account is null)
        {
            return;
        }

        var outcome = account.ReversePending(
            _guids.NewSequentialGuid(),
            integrationEvent.GrantReference,
            integrationEvent.Reason,
            _clock.UtcNow);

        if (outcome is RewardReversalOutcome.AlreadyDecided or RewardReversalOutcome.NotFound)
        {
            return;
        }

        if (outcome == RewardReversalOutcome.Declined)
        {
            _logger.LogInformation(
                "Reward grant {GrantReference} had vested and stays earned (D-9).",
                integrationEvent.GrantReference);
        }

        _accounts.Update(account);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
