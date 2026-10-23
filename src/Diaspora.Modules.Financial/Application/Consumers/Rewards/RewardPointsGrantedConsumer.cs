using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Consumers.Rewards;

/// <summary>
/// Awards points a grantor has recognised.
/// <para>
/// <b>This is the seam that lets a reputation engine, a creator platform or a challenge award points
/// without owning money.</b> The grantor publishes the fact; the Financial Platform records it and is
/// the only thing that ever decides what it is worth. That is the same separation B15 keeps
/// everywhere else, and it is what makes points-to-money a single, auditable crossing rather than an
/// arithmetic anybody could do.
/// </para>
/// <para>
/// <b>Idempotent on the grantor's own reference, and silently so.</b> The bus is at-least-once; a
/// redelivered grant awards nothing and answers success, because a consumer that threw on its second
/// delivery would be redelivered for ever. The reference is uniquely indexed per account, so two
/// workers racing the same delivery lose one at the database rather than both awarding.
/// </para>
/// <para>
/// <b>The account is opened on the first grant.</b> A member who has never earned anything has no
/// row, which is what makes "you have no points" a zero rather than a 404 on the read side.
/// </para>
/// </summary>
public sealed class RewardPointsGrantedConsumer
    : IntegrationEventHandler<RewardPointsGrantedIntegrationEvent>
{
    private readonly IRewardAccountRepository _accounts;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RewardPointsGrantedConsumer(
        IRewardAccountRepository accounts,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        RewardPointsGrantedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.Points <= 0
            || string.IsNullOrWhiteSpace(integrationEvent.GrantReference))
        {
            return;
        }

        var account = await _accounts.FindByMemberAsync(
            integrationEvent.MemberUserId, cancellationToken);

        if (account is null)
        {
            account = RewardAccount.Open(
                RewardAccountId.From(_guids.NewSequentialGuid()),
                integrationEvent.MemberUserId,
                _clock.UtcNow);

            _accounts.Add(account);
        }

        var awarded = account.Grant(
            _guids.NewSequentialGuid(),
            integrationEvent.Points,
            integrationEvent.Source,
            integrationEvent.GrantReference,
            integrationEvent.GrantedAt,
            policyVersion: integrationEvent.PolicyVersion);

        if (!awarded)
        {
            return;
        }

        _accounts.Update(account);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
