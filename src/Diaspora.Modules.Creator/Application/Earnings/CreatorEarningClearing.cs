using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.Modules.Financial.Extensibility.Disbursements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Creator.Application.Earnings;

/// <summary>
/// Pays cleared earnings into their creators' wallets.
/// <para>
/// <b>The step that was missing, and the reason a creator could see money they could never reach.</b>
/// An earning is recorded as a claim when a donation completes — correctly, since it has not cleared
/// — and the dashboard states the date it clears. Nothing kept that promise:
/// <c>MakeAvailable</c>, <c>AttachToPayout</c> and <c>MarkPaid</c> had no caller outside their own
/// tests, and <c>ListClearableAsync</c> was written and never asked. A creator's £1,000 read PENDING
/// and would have read PENDING on the clearing date and every day after it, beside a Withdraw button
/// with nothing behind it.
/// </para>
/// <para>
/// <b>The money moves first and the state second, and that order is the design.</b> B15 posts
/// platform revenue into the member's own wallet, keyed on <c>earning:{id}</c>, so a pass that dies
/// between the two does not pay twice: the next pass replays the posting — which the ledger answers
/// rather than repeats — and marks the row. The other order would tell a member the money was in
/// their wallet before it was.
/// </para>
/// <para>
/// <b>Its own class rather than a method on the sweep</b>, so it can be tested without standing up a
/// hosted service, a distributed lock and five repositories that have nothing to do with money.
/// </para>
/// </summary>
public sealed class CreatorEarningClearing
{
    private readonly ICreatorEarningRepository _earnings;
    private readonly IPlatformDisbursement _disbursements;
    private readonly CreatorRevenueShareOptions _shares;
    private readonly ILogger<CreatorEarningClearing> _logger;

    public CreatorEarningClearing(
        ICreatorEarningRepository earnings,
        IPlatformDisbursement disbursements,
        IOptions<CreatorRevenueShareOptions> shares,
        ILogger<CreatorEarningClearing> logger)
    {
        ArgumentNullException.ThrowIfNull(shares);

        _earnings = earnings;
        _disbursements = disbursements;
        _shares = shares.Value;
        _logger = logger;
    }

    /// <summary>
    /// Pays every earning past its clearing period and marks it available. Answers how many moved.
    /// <para>
    /// <b>One earning failing does not stop the batch.</b> A frozen wallet, a currency the platform
    /// holds no revenue in — each is one member's problem, and abandoning the pass would hold up
    /// everybody else's money behind it. The row stays pending and the next pass tries again.
    /// </para>
    /// <para>
    /// Stages; does not save. The caller owns the unit of work, as everything else in the sweep does.
    /// </para>
    /// </summary>
    public async Task<int> ClearAsync(
        DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
    {
        // The same period the response tells the member their earning clears after, read from the
        // same options. A second copy of "thirty days" here is how a screen and a sweep come to
        // disagree about a date the member is holding the platform to.
        var clearedBefore = now.AddDays(-Math.Max(0, _shares.ClearingDays));

        var cleared = 0;

        foreach (var earning in
            await _earnings.ListClearableAsync(clearedBefore, batchSize, cancellationToken))
        {
            // The creator's share, not the gross. The gross is what the donor gave; the platform's
            // cut, where it takes one, never belonged to the creator and must not be paid out.
            var paid = await _disbursements.PayAsync(
                new PlatformDisbursementRequest
                {
                    BeneficiaryUserId = earning.UserId,
                    AmountMinorUnits = earning.AmountMinorUnits,
                    Currency = earning.Currency,
                    Exponent = earning.Exponent,
                    Reference = $"earning:{earning.Id.Value:N}",
                    Description = "Creator earnings",
                    Reason = $"Creator earning cleared: {earning.SourceReference}"
                },
                cancellationToken);

            if (paid.IsFailure)
            {
                _logger.LogWarning(
                    "Creator earning {EarningId} could not be paid: {Code} {Description}",
                    earning.Id.Value,
                    paid.Error.Code,
                    paid.Error.Description);

                continue;
            }

            var available = earning.MakeAvailable(now);

            if (available.IsFailure)
            {
                // Not pending any more — reversed by a refund between the query and here. The money
                // has moved, so this is worth seeing rather than swallowing.
                _logger.LogWarning(
                    "Creator earning {EarningId} was paid but is {State} and cannot be marked available",
                    earning.Id.Value,
                    earning.State);

                continue;
            }

            cleared++;
        }

        return cleared;
    }
}
