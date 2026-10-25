using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Makes pending reward points spendable when their vesting window ends (D-9, RWD-07).
/// <para>
/// <b>Not while the member is held.</b> A hold placed during the window keeps the points pending,
/// and so still reversible, until it is lifted; the account is picked up again on a later run. The
/// same rule as <see cref="RewardConversionReleaseSweep"/>, for the same reason.
/// </para>
/// <para>
/// <b>Each account is its own unit of work</b>, and the account's revision stops two runs vesting
/// the same grant twice.
/// </para>
/// </summary>
public sealed class RewardVestingSweep : FinancialSweep
{
    public RewardVestingSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<RewardVestingSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableRewardVestingSweep;

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Options.RewardVestingSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RewardAccountId> due;

        using (var scope = Scopes.CreateScope())
        {
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            due = await scope.ServiceProvider
                .GetRequiredService<IRewardAccountRepository>()
                .ListDueForVestingAsync(clock.UtcNow, Options.SweepBatchSize, cancellationToken);
        }

        var vested = 0;

        foreach (var accountId in due)
        {
            if (await TryVestAsync(accountId, cancellationToken))
            {
                vested++;
            }
        }

        return vested;
    }

    private async Task<bool> TryVestAsync(RewardAccountId accountId, CancellationToken cancellationToken)
    {
        using var scope = Scopes.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var account = await accounts.FindAsync(accountId, cancellationToken);

        if (account is null || account.IsConversionBlocked)
        {
            return false;
        }

        if (account.VestDue(clock.UtcNow) == 0)
        {
            return false;
        }

        accounts.Update(account);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another run, a grant or a conversion changed the account first. Whatever is still due
            // is picked up on the next run.
            Logger.LogInformation("Reward account {AccountId} changed while vesting; retrying later.",
                accountId.Value);

            return false;
        }

        return true;
    }
}
