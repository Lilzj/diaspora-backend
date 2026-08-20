using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Hosting;

/// <summary>
/// Lapses memberships whose renewal date has passed, and repairs the counters they move.
/// <para>
/// <b>Lapsing is not removal, and the distinction is the reason this exists.</b> A member whose
/// annual subscription ran out has not been thrown out and has not walked away — they have simply
/// stopped being current, and can renew. Recording that as a departure would make an organization's
/// history unreadable and would count against the member.
/// </para>
/// <para>
/// <b>The counters are reconciled from a recount rather than moved by hand.</b> This is the repair
/// path the aggregate's <c>Reconcile</c> methods exist for: a sweep applying deltas would compound
/// any drift it inherited, whereas a recount is idempotent and fixes whatever went wrong earlier.
/// The lapsed memberships settle their own deltas through the ordinary status handler; the recount
/// afterwards is what makes the numbers right rather than merely consistent.
/// </para>
/// <para>
/// Runs hourly at most. A membership that lapses twelve hours late costs nobody anything, and a
/// sweep on a tighter loop would recount every affected organization for no benefit.
/// </para>
/// </summary>
public sealed class CommunityLifeMembershipSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly ILogger<CommunityLifeMembershipSweep> _logger;
    private readonly IClock _clock;

    public CommunityLifeMembershipSweep(
        IServiceScopeFactory scopes,
        IOptions<CommunityLifeOptions> options,
        ILogger<CommunityLifeMembershipSweep> logger,
        IClock clock)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableMembershipSweep)
        {
            _logger.LogInformation("CommunityLifeMembershipSweep is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromHours(
            Math.Max(1, _options.Value.MembershipSweepHours));

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One pass. Public so a test can run it without waiting on a timer.</summary>
    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            await LapseAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A failed pass must not stop the timer.
        catch (Exception exception)
        {
            _logger.LogError(exception, "The Community Life membership sweep failed.");
        }
#pragma warning restore CA1031
    }

    private async Task LapseAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var memberships = services.GetRequiredService<IOrganizationMembershipRepository>();
        var organizations = services.GetRequiredService<IOrganizationRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();

        var now = _clock.UtcNow;
        var batchSize = Math.Clamp(_options.Value.SweepBatchSize, 1, 1_000);

        var lapsed = await memberships.FindLapsedAsync(now, batchSize, cancellationToken);

        if (lapsed.Count == 0)
        {
            return;
        }

        var affected = new HashSet<OrganizationId>();

        foreach (var membership in lapsed)
        {
            var moved = membership.MoveTo(
                MembershipStatus.Lapsed, guids.NewSequentialGuid(), now);

            if (moved.IsSuccess)
            {
                memberships.Update(membership);
                affected.Add(membership.OrganizationId);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ReconcileAsync(
            memberships, organizations, unitOfWork, affected, now, cancellationToken);
    }

    /// <summary>
    /// Restates each affected body's member counters from a recount.
    /// <para>
    /// One grouped query per organization, and only for those a lapse touched — so a sweep that
    /// found nothing costs nothing, and one that lapsed forty memberships across three bodies
    /// recounts three.
    /// </para>
    /// </summary>
    private static async Task ReconcileAsync(
        IOrganizationMembershipRepository memberships,
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        HashSet<OrganizationId> affected,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (affected.Count == 0)
        {
            return;
        }

        foreach (var organizationId in affected)
        {
            var organization = await organizations.GetAsync(organizationId, cancellationToken);

            if (organization is null)
            {
                continue;
            }

            var byStatus = await memberships.CountByStatusAsync(
                organizationId, cancellationToken);

            var administrators = await memberships.CountByRoleAsync(
                organizationId, MembershipRole.Administrator, cancellationToken);

            var volunteers = await memberships.CountByRoleAsync(
                organizationId, MembershipRole.Volunteer, cancellationToken);

            organization.ReconcileStatistics(
                byStatus.GetValueOrDefault(MembershipStatus.Active),
                byStatus.GetValueOrDefault(MembershipStatus.Requested)
                    + byStatus.GetValueOrDefault(MembershipStatus.Invited),
                administrators,
                volunteers,
                now);

            organizations.Update(organization);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
