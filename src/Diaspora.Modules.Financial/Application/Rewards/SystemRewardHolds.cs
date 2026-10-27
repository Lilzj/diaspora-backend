using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>
/// Holds a member's points for review because a signal asked for it, decided by nobody (D-13).
/// <para>
/// <b>One way to place a system hold</b>, for every signal that places one: a shared device (CS-D5)
/// and a member's Trust risk becoming High (CS-4b). Points still accrue and vest; nothing converts,
/// and no converted credit is released, until staff lift it from the review queue
/// (<c>GET /operations/rewards/holds</c>) or an appeal does. The note is an operator's and never
/// reaches the member.
/// </para>
/// <para>
/// <b>A hold already in force is left alone.</b> A second signal, a redelivery, or a hold staff placed
/// for their own reason keeps the note it has: the platform never writes over a person.
/// </para>
/// </summary>
public sealed class SystemRewardHolds
{
    private readonly IRewardAccountRepository _accounts;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SystemRewardHolds(
        IRewardAccountRepository accounts, IGuidGenerator guids, IClock clock, IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Holds the member's points with the operator's note. False when a hold was already in force.</summary>
    public async Task<bool> PlaceAsync(
        Guid memberUserId, string reason, CancellationToken cancellationToken)
    {
        var account = await _accounts.FindByMemberAsync(memberUserId, cancellationToken);

        if (account is { IsConversionBlocked: true })
        {
            return false;
        }

        var now = _clock.UtcNow;

        // A member with no points yet is held all the same, as a staff hold opens the account: the
        // first points they earn must not convert before the review.
        if (account is null)
        {
            account = RewardAccount.Open(
                RewardAccountId.From(_guids.NewSequentialGuid()), memberUserId, now);

            _accounts.Add(account);
        }

        account.BlockConversion(_guids.NewSequentialGuid(), reason, decidedByUserId: null, now);
        _accounts.Update(account);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
