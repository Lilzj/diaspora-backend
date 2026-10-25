using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Moves converted reward credit from pending to available when its hold ends (D-7).
/// <para>
/// <b>Not while the member is held.</b> A hold placed during the window, by staff or by a review
/// signal, keeps the credit pending until it is lifted; that is what gives a review the time it
/// needs before money can leave. The conversion is simply picked up again on a later run.
/// </para>
/// <para>
/// <b>Each release is its own unit of work.</b> One conversion that cannot be released (a frozen
/// wallet, a concurrent run that got there first) does not hold the rest back, and the conversion's
/// own revision stops two runs releasing the same credit twice.
/// </para>
/// </summary>
public sealed class RewardConversionReleaseSweep : FinancialSweep
{
    public RewardConversionReleaseSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<RewardConversionReleaseSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableRewardConversionReleaseSweep;

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Options.RewardConversionReleaseSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        List<RewardConversion> due;

        using (var scope = Scopes.CreateScope())
        {
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            due = [.. await scope.ServiceProvider
                .GetRequiredService<IRewardConversionRepository>()
                .ListDueForReleaseAsync(clock.UtcNow, Options.SweepBatchSize, cancellationToken)];
        }

        var released = 0;

        foreach (var conversion in due)
        {
            if (await TryReleaseAsync(conversion.Id, cancellationToken))
            {
                released++;
            }
        }

        return released;
    }

    private async Task<bool> TryReleaseAsync(
        Domain.Identifiers.RewardConversionId conversionId, CancellationToken cancellationToken)
    {
        using var scope = Scopes.CreateScope();

        var conversions = scope.ServiceProvider.GetRequiredService<IRewardConversionRepository>();
        var accounts = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerPostingService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var conversion = await conversions.FindAsync(conversionId, cancellationToken);

        if (conversion is null || !conversion.IsHolding)
        {
            return false;
        }

        var standing = await accounts.FindStandingAsync(conversion.MemberUserId, cancellationToken);

        if (standing is { IsConversionBlocked: true })
        {
            return false;
        }

        var posting = await ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionType.RewardConversion,
                Amount = conversion.Credited,
                Description = "Converted reward credit released from its hold",
                Audit = AuditStamp.Create(
                    FinancialActor.System(nameof(RewardConversionReleaseSweep)),
                    FinancialSource.System,
                    "The conversion's hold ended.",
                    policyVersion: conversion.PolicyVersion),
                Legs =
                [
                    new LedgerPostingLeg
                    {
                        WalletId = conversion.WalletId,
                        Direction = LedgerDirection.Debit,
                        Amount = conversion.Credited,
                        Bucket = LedgerBucket.Pending,
                        EntryType = LedgerEntryType.Release
                    },
                    new LedgerPostingLeg
                    {
                        WalletId = conversion.WalletId,
                        Direction = LedgerDirection.Credit,
                        Amount = conversion.Credited,
                        Bucket = LedgerBucket.Available,
                        EntryType = LedgerEntryType.Release
                    }
                ],
                Reference = $"reward-conversion-release:{conversion.Id.Value:N}"
            },
            cancellationToken);

        if (posting.IsFailure)
        {
            Logger.LogWarning(
                "Reward conversion {ConversionId} was not released: {Error}",
                conversion.Id.Value,
                posting.Error.Description);

            return false;
        }

        conversion.Release(clock.UtcNow);
        conversions.Update(conversion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another run released it first; its posting stands and this one rolled back.
            return false;
        }

        return true;
    }
}
