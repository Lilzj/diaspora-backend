using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Consumers.Appeals;

/// <summary>
/// Undoes a staff decision about a member's money when Trust upholds their appeal against it.
/// <para>
/// <b>One consumer for every appeal this module answers,</b> because a module settles an event once
/// (DSP: one module, one owner). Two kinds:
/// </para>
/// <list type="bullet">
/// <item><b>RewardHold</b> (RWD-14, CS-4a): lifts the member's points hold, as a lift from the review
/// queue is, and announces it so a shared-device flag clears in Creator too.</item>
/// <item><b>Money</b> (7 Nov 2026): a freeze of the member's own wallet is lifted, with the reviewer's
/// explanation as the reason the member reads; or a refund request the member made and staff turned
/// down is reopened for a new decision by somebody else.</item>
/// </list>
/// <para>
/// <b>Trust cannot see wallets or refund requests, so it does not check a Money target.</b> This does:
/// the wallet must be the appellant's own personal wallet and frozen; the refund request must be the
/// appellant's and refused. Anything else is ignored. An appeal approved against somebody else's wallet
/// changes nothing, which is the only safe answer to a target nobody verified.
/// </para>
/// <para>
/// A rejected appeal changes nothing here; the appeal keeps the decision.
/// </para>
/// </summary>
public sealed class FinancialAppealApprovedConsumer
    : IntegrationEventHandler<AppealApprovedIntegrationEvent>
{
    public const string RewardHoldAppealType = "RewardHold";

    public const string MoneyAppealType = "Money";

    /// <summary>The reason recorded on a lifted points hold.</summary>
    public const string Reason = "Appeal approved.";

    /// <summary>
    /// The reason a lifted wallet freeze carries when the reviewer wrote no explanation. Trust requires
    /// one on every decision, so this is a guard against an older event, not the expected path.
    /// </summary>
    public const string WalletUnfrozenReason = "Your appeal against the freeze was upheld.";

    private readonly IRewardAccountRepository _accounts;
    private readonly IWalletRepository _wallets;
    private readonly IRefundRequestRepository _refundRequests;
    private readonly FinancialIntegrationEventRelay _relay;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FinancialAppealApprovedConsumer> _logger;

    public FinancialAppealApprovedConsumer(
        IRewardAccountRepository accounts,
        IWalletRepository wallets,
        IRefundRequestRepository refundRequests,
        FinancialIntegrationEventRelay relay,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<FinancialAppealApprovedConsumer> logger)
    {
        _accounts = accounts;
        _wallets = wallets;
        _refundRequests = refundRequests;
        _relay = relay;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override Task HandleAsync(
        AppealApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.AppealType switch
        {
            RewardHoldAppealType => LiftRewardHoldAsync(integrationEvent, cancellationToken),
            MoneyAppealType => UndoMoneyDecisionAsync(integrationEvent, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    private async Task LiftRewardHoldAsync(
        AppealApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// The target is a wallet id or a refund request id; the two cannot collide, so the wallet is
    /// looked for first and the request second.
    /// </summary>
    private async Task UndoMoneyDecisionAsync(
        AppealApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(integrationEvent.TargetId, out var targetId))
        {
            _logger.LogWarning(
                "Money appeal {AppealId} names '{TargetId}', which is not an id. Nothing changed.",
                integrationEvent.AppealId,
                integrationEvent.TargetId);

            return;
        }

        var now = _clock.UtcNow;

        var wallet = await _wallets.FindAsync(WalletId.From(targetId), cancellationToken);

        if (wallet is not null)
        {
            if (wallet.OwnerType != FinancialAccountType.Personal
                || wallet.OwnerId != integrationEvent.UserId
                || wallet.Status != WalletStatus.Frozen)
            {
                _logger.LogWarning(
                    "Money appeal {AppealId} was upheld against wallet {WalletId}, which is not a frozen "
                    + "wallet of the appellant's. Nothing changed.",
                    integrationEvent.AppealId,
                    targetId);

                return;
            }

            wallet.Activate(
                now,
                string.IsNullOrWhiteSpace(integrationEvent.Explanation)
                    ? WalletUnfrozenReason
                    : integrationEvent.Explanation.Trim());

            _wallets.Update(wallet);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        var request = await _refundRequests.FindAsync(RefundRequestId.From(targetId), cancellationToken);

        if (request is null
            || request.RequestedByUserId != integrationEvent.UserId
            || request.Status != RefundRequestStatus.Rejected)
        {
            _logger.LogWarning(
                "Money appeal {AppealId} was upheld against {TargetId}, which is neither a frozen wallet "
                + "nor a refused refund request of the appellant's. Nothing changed.",
                integrationEvent.AppealId,
                targetId);

            return;
        }

        // Only one request may be open on a payment. A member who asked again after the refusal
        // already has their new decision coming; reopening the old one would collide with it.
        if (await _refundRequests.FindOpenAsync(request.PaymentIntentId, cancellationToken) is not null)
        {
            _logger.LogWarning(
                "Money appeal {AppealId} was upheld against refund request {RefundRequestId}, but another "
                + "request on the same payment is already open. The open one will be decided instead.",
                integrationEvent.AppealId,
                targetId);

            return;
        }

        request.ReopenOnAppeal(now);
        _refundRequests.Update(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
