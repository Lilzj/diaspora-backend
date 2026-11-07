using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Consumers;

/// <summary>
/// Raises a suspension for two members of staff to review when a member reaches a third strike
/// (7 Nov 2026).
/// <para>
/// <b>The owner's strike ladder:</b> a warning, then a week without posting in the area, then thirty
/// days, then a suspension reviewed by two members of staff. Trust gives the strikes and announces
/// the third; the account is Identity's, so this raises the suspension here, as a proposal with
/// nobody's name on it. It suspends nobody: one member of staff takes it up as their own, writing
/// the note the member reads, and a second confirms it — or either declines it.
/// </para>
/// </summary>
public sealed class ThirdStrikeSuspensionConsumer
    : IntegrationEventHandler<MemberReachedThirdStrikeIntegrationEvent>
{
    // Enough to see whether this case has raised one already, however the earlier one ended.
    private const int EarlierProposalsRead = 50;

    private readonly IUserRepository _users;
    private readonly IAccountActionProposalRepository _proposals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ThirdStrikeSuspensionConsumer(
        IUserRepository users,
        IAccountActionProposalRepository proposals,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _proposals = proposals;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        MemberReachedThirdStrikeIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var target = UserId.From(integrationEvent.UserId);
        var user = await _users.GetByIdAsync(target, cancellationToken);

        // Gone, erased, or already suspended or closed: there is nothing to propose.
        if (user is null || user.IsAnonymized || !user.AccessState.CanTransitionTo(AccessState.Suspended))
        {
            return;
        }

        // Somebody has already proposed suspending them; the two would be decided as one.
        if (await _proposals.HasPendingAsync(target, AccountActionKind.Suspend, cancellationToken))
        {
            return;
        }

        // Once per case, however often the event is delivered and however the earlier one ended.
        var earlier = await _proposals.ListAsync(null, target, EarlierProposalsRead, cancellationToken);
        if (earlier.Any(proposal => proposal.RaisedByCaseId == integrationEvent.CaseId))
        {
            return;
        }

        var reason = ModerationReasons.TryParse(integrationEvent.ReasonCode, out var code)
            ? ModerationReasons.Label(code)
            : integrationEvent.ReasonCode;

        var note =
            $"Third strike in {integrationEvent.Area}: content removed for being {reason}. " +
            "Raised by the platform; a member of staff must take it up before a second confirms it.";

        await _proposals.AddAsync(
            AccountActionProposal.Raise(target, integrationEvent.CaseId, integrationEvent.ReasonCode, note, _clock.UtcNow),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
