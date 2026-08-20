using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Modules.Creator.Application.Services;

/// <summary>
/// Re-decides a creator's status, and is the only thing that does.
/// <para>
/// <b>One caller of <c>CreatorStatusPolicy.Resolve</c> outside the policy's own tests</b>, so a
/// status can never be set by a handler that thought it knew better. Every path that could change
/// the answer — a recognised action, a verification decision, a follower count, a suspension —
/// finishes here.
/// </para>
/// <para>
/// <b>Trust's account standing is folded in before the ladder is consulted.</b> A member Trust has
/// suspended is a suspended creator whatever this module's own flags say, which is the boundary
/// PSR-1.2 added <c>GetAccountStandingAsync</c> for: "the platform's suspensions reached exactly the
/// surfaces Trust's own rule chain was consulted on".
/// </para>
/// <para>
/// <b>The event is raised by the aggregate and only on a real change.</b> This service asks the
/// aggregate to apply a decision; <c>CreatorProfile.ApplyStatus</c> raises
/// <c>CreatorStatusChangedDomainEvent</c> when the served status actually moved and stays silent
/// otherwise. §23: "Do not publish events merely because an endpoint exists."
/// </para>
/// </summary>
public sealed class CreatorStatusService
{
    private readonly ICreatorProfileRepository _creators;
    private readonly ICreatorVerificationRepository _verifications;
    private readonly ITrustService _trust;
    private readonly IClock _clock;

    public CreatorStatusService(
        ICreatorProfileRepository creators,
        ICreatorVerificationRepository verifications,
        ITrustService trust,
        IClock clock)
    {
        _creators = creators;
        _verifications = verifications;
        _trust = trust;
        _clock = clock;
    }

    /// <summary>
    /// Brings a creator's verification standing and status up to date.
    /// <para>
    /// Stages the write; the calling handler saves. Returns true when the served status moved, which
    /// callers use only for telemetry — the event itself is the aggregate's.
    /// </para>
    /// </summary>
    public async Task<bool> RefreshAsync(
        CreatorProfile creator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var now = _clock.UtcNow;

        var application = await _verifications.FindForCreatorAsync(creator.Id, cancellationToken);

        var isVerified = application is not null
            && CreatorVerificationTransitions.ConfersVerifiedCreator(application.State)
            && (application.ExpiresAt is not { } expiry || expiry > now);

        var inReview = application is not null
            && CreatorVerificationTransitions.IsWithReviewer(application.State);

        creator.ApplyVerificationStanding(isVerified, inReview, now);

        await ApplyTrustStandingAsync(creator, now, cancellationToken);

        var (earned, served) = CreatorStatusPolicy.Resolve(creator);

        var moved = creator.ApplyStatus(earned, served, now);

        _creators.Update(creator);

        return moved;
    }

    /// <summary>
    /// Folds Trust's opinion into the creator's standing.
    /// <para>
    /// <b>Trust's word overrides this module's flags in one direction only.</b> A member Trust has
    /// suspended is suspended here; a member Trust considers fine is <em>not</em> thereby un-suspended
    /// by this module, because a creator suspension is a creator-programme decision Trust knows
    /// nothing about. Reading the answer either way round would make one of the two systems unable
    /// to act.
    /// </para>
    /// </summary>
    private async Task ApplyTrustStandingAsync(
        CreatorProfile creator, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var standing = await _trust.GetAccountStandingAsync(
            SharedKernel.Identity.UserId.From(creator.UserId), cancellationToken);

        switch (standing)
        {
            case AccountStanding.Suspended when !creator.IsSuspended:
                creator.ApplyStanding(
                    creator.IsRestricted,
                    isSuspended: true,
                    "Your account is suspended.",
                    now);
                break;

            case AccountStanding.Restricted when !creator.IsRestricted && !creator.IsSuspended:
                creator.ApplyStanding(
                    isRestricted: true,
                    isSuspended: false,
                    "Your account is limited at the moment.",
                    now);
                break;

            case AccountStanding.Good:
            case AccountStanding.Unknown:
            case AccountStanding.UnderReview:
            default:
                // No opinion, a good one, or a queue. None of the three lifts a creator-programme
                // restriction — that is this module's decision to make and to reverse. UnderReview
                // is named rather than left to the default because the enum's own documentation
                // warns that it is not a restriction and must not be treated as one by ordering.
                break;
        }
    }
}
