using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Moves campaigns past their deadline and stamps milestones the totals have overtaken.
/// <para>
/// <b>Both are corrections rather than the primary path.</b> A milestone is stamped the moment the
/// donation that passes it completes, inside the capture handler; this catches the cases that path
/// cannot see — a total corrected downwards and then upwards again by the reconciliation sweep, a
/// milestone declared while the platform was down, a donation whose capture event was lost.
/// </para>
/// <para>
/// <b>A deadline moves a campaign and closes its cause in one write.</b> A campaign that said
/// <c>DeadlineReached</c> while its cause still took money would keep collecting after the date it
/// promised to stop, which is a promise to donors rather than a display detail.
/// </para>
/// <para>
/// Takes B17's lease before touching anything, so three instances do not do the same work three
/// times.
/// </para>
/// </summary>
public sealed class GivingLifecycleSweep : FinancialSweep
{
    private const string LockKey = "financial:campaign-lifecycle";

    public GivingLifecycleSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<GivingLifecycleSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableCampaignSweep;

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Max(1, Options.CampaignSweepMinutes));

    public override Task<int> RunOnceAsync(CancellationToken cancellationToken = default) =>
        RunOnceAsync(asOf: null, cancellationToken);

    /// <summary>
    /// One pass, as at a given moment.
    /// <para>
    /// The moment is a parameter so a test can say "the deadline has passed" without a mutable clock
    /// registered host-wide, which would move token expiry and every other deadline with it. Null
    /// means now, and nothing in production passes anything else.
    /// </para>
    /// </summary>
    public async Task<int> RunOnceAsync(
        DateTimeOffset? asOf, CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(Math.Max(6, Options.CampaignSweepMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            Logger.LogDebug("Another instance holds the campaign lifecycle lease.");

            return 0;
        }

        try
        {
            var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
            var causes = scope.ServiceProvider.GetRequiredService<IDonationCauseRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var now = asOf ?? clock.UtcNow;
            var handled = 0;

            foreach (var campaign in await campaigns.ListPastDeadlineAsync(
                now, Options.SweepBatchSize, cancellationToken))
            {
                if (!MoveToDeadlineReached(campaign, now))
                {
                    continue;
                }

                campaigns.Update(campaign);

                if (campaign.CauseId is { } causeId
                    && await causes.FindAsync(causeId, cancellationToken) is { IsActive: true } cause)
                {
                    cause.Close(now);
                    causes.Update(cause);
                }

                handled++;
            }

            foreach (var campaign in await campaigns.ListWithOpenMilestonesAsync(
                Options.SweepBatchSize, cancellationToken))
            {
                if (campaign.CauseId is not { } causeId
                    || await causes.FindAsync(causeId, cancellationToken) is not { } cause)
                {
                    continue;
                }

                if (campaign.EvaluateMilestones(cause.RaisedMinorUnits, now).Count == 0)
                {
                    continue;
                }

                campaigns.Update(campaign);
                handled++;
            }

            if (handled > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return handled;
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Walks a campaign to <see cref="CampaignStatus.DeadlineReached"/> along legal moves only.
    /// <para>
    /// <b>Only <c>Active</c> reaches it directly, and the sweep's input is wider than that.</b> The
    /// transition table — which is the client's, transcribed — permits <c>Published → Active</c> and
    /// <c>GoalReached → Active</c>, and <c>Active → DeadlineReached</c>. A campaign that was published
    /// and never separately marked active, or one sitting past its goal, therefore takes two hops
    /// rather than none. The first version of this sweep tried the single move, the table refused it,
    /// and the sweep skipped the row — so a campaign kept collecting past the date it had promised to
    /// stop, silently.
    /// </para>
    /// <para>
    /// <b><c>GoalReached</c> is deliberately not walked to <c>Completed</c>.</b> "Finished, and the
    /// money was used" is the organizer's statement about what they did, not a conclusion a clock is
    /// entitled to draw.
    /// </para>
    /// </summary>
    private static bool MoveToDeadlineReached(Campaign campaign, DateTimeOffset now)
    {
        const string reason = "The campaign's deadline passed.";

        if (campaign.Status is CampaignStatus.Published or CampaignStatus.GoalReached
            && campaign.MoveTo(CampaignStatus.Active, now, reason).IsFailure)
        {
            return false;
        }

        return campaign.MoveTo(CampaignStatus.DeadlineReached, now, reason).IsSuccess;
    }
}

/// <summary>
/// Re-announces membership payment bindings whose activation may never have landed.
/// <para>
/// <b>This is the recoverable half of COMMUNITY-LIFE-GAP-003, and the register asked for it by
/// name.</b> The binding is written and announced in one transaction; the membership is activated by
/// the module that owns it, in a second. If the second fails — a crash, a deploy, a database blip —
/// the money is captured, the binding exists, and the member is not a member. That is the exact
/// failure "money captured, membership silently lost" describes.
/// </para>
/// <para>
/// <b>The repair is a replay, not a second binding.</b> Nothing new is written and no money moves;
/// the same settlement fact is published again, and the consumer's activation is idempotent — a
/// membership already active moves to active and does nothing. At-least-once delivery is the
/// platform's contract, so a consumer that could not tolerate a replay was already broken.
/// </para>
/// <para>
/// <b>Bounded.</b> A binding is re-announced at most
/// <c>MembershipBindingMaxReannouncements</c> times. A membership nothing can activate — an
/// organization that was deleted, a member who left — must stop being shouted about, and the count
/// on the row is what an operator reads to find those.
/// </para>
/// </summary>
public sealed class MembershipBindingReconciliationSweep : FinancialSweep
{
    private const string LockKey = "financial:membership-binding-reconciliation";

    public MembershipBindingReconciliationSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<MembershipBindingReconciliationSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableMembershipBindingReconciliation;

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Max(1, Options.MembershipBindingReconciliationAfterMinutes));

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(
                    Math.Max(6, Options.MembershipBindingReconciliationAfterMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            Logger.LogDebug("Another instance holds the membership reconciliation lease.");

            return 0;
        }

        try
        {
            var bindings =
                scope.ServiceProvider.GetRequiredService<IMembershipPaymentBindingRepository>();
            var authority = scope.ServiceProvider
                .GetService<Extensibility.Memberships.IMembershipSubjectAuthority>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;

            var stale = await bindings.ListForReconciliationAsync(
                now.AddMinutes(-Options.MembershipBindingReconciliationAfterMinutes),
                Options.MembershipBindingMaxReannouncements,
                Options.SweepBatchSize,
                cancellationToken);

            var handled = 0;

            foreach (var binding in stale)
            {
                // Where the owning module can be asked, ask it: a membership that is no longer
                // waiting for a payment has been activated, and re-announcing it would be noise.
                // Where nothing answers, announce anyway — a deployment with no consumer is exactly
                // the one whose activations never landed.
                if (authority is not null)
                {
                    var facts = await authority.DescribeAsync(
                        binding.MembershipId, cancellationToken);

                    if (facts is not null && !facts.IsAwaitingPayment)
                    {
                        continue;
                    }
                }

                binding.Reannounce(now);
                bindings.Update(binding);
                handled++;

                Logger.LogWarning(
                    "Membership {Membership} was paid for at {BoundAt} and is still awaiting "
                    + "activation. Re-announcing the settlement (attempt {Attempt}).",
                    binding.MembershipId,
                    binding.CreatedAt,
                    binding.ReannounceCount);
            }

            if (handled > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return handled;
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }
}
