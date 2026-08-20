using System.Globalization;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.Wallets;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Extensibility.Automation;

/// <summary>
/// B15's answer to the two automation conditions about money — B23.x Wave 12.
/// <para>
/// <b>The Automation Platform declared the question; this module answers it.</b>
/// <c>walletBalance</c> and <c>subscriptionActive</c> are conditions a member can put on an
/// automation, and no trigger payload may carry either — a balance is not a fact about an event, and
/// an entitlement supplied by a payload would be an entitlement any publishing module could grant.
/// Wave 11 shipped both declared unevaluable, failing closed with the words "a balance is B15's and
/// this module has no read port for one". This is the read port, on B15's side of the boundary.
/// </para>
/// <para>
/// <b>It answers, and it never acts.</b> There is no write here, no command dispatched, no ledger
/// entry, no reservation. The automation platform cannot move money, and the seam it reaches this
/// module through is read-only by construction — <c>ReadAsync</c> is the whole interface.
/// </para>
/// <para>
/// <b>The member is always the automation's own owner, resolved by the engine.</b> Nothing in a rule
/// or a payload can name whose balance is read, and this implementation reads only the member the
/// engine passed.
/// </para>
/// </summary>
public sealed class FinancialAutomationConditionSource : IAutomationConditionSource
{
    private readonly IWalletRepository _wallets;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ILogger<FinancialAutomationConditionSource> _logger;

    public FinancialAutomationConditionSource(
        IWalletRepository wallets,
        ISubscriptionRepository subscriptions,
        ILogger<FinancialAutomationConditionSource> logger)
    {
        _wallets = wallets;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public IReadOnlyList<string> Answers { get; } =
    [
        AutomationConditionKeys.WalletBalance,
        AutomationConditionKeys.SubscriptionActive
    ];

    public async Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        AutomationConditionQuery request, CancellationToken cancellationToken = default)
    {
        var answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (request.Member.Value == Guid.Empty)
        {
            return answers;
        }

        if (request.ConditionKeys.Contains(
                AutomationConditionKeys.WalletBalance, StringComparer.Ordinal))
        {
            await ReadBalanceAsync(request, answers, cancellationToken);
        }

        if (request.ConditionKeys.Contains(
                AutomationConditionKeys.SubscriptionActive, StringComparer.Ordinal))
        {
            var subscription = await _subscriptions.FindActiveForSubscriberAsync(
                request.Member.Value, cancellationToken);

            // `GrantsAccess` rather than a status comparison. A subscription that has ended grants
            // nothing whatever its rows still say, and the aggregate is the one place that decides.
            answers[AutomationConditionKeys.SubscriptionActive] =
                subscription is { GrantsAccess: true } ? "true" : "false";
        }

        return answers;
    }

    /// <summary>
    /// The member's spendable balance.
    /// <para>
    /// <b>Primary wallets only, and summed across currencies is not something this does.</b> A member
    /// with a GBP wallet and an NGN wallet has two balances and no single number; adding them would
    /// invent an exchange rate, and this platform's standing rule is that a figure is never converted.
    /// Where a member holds more than one, the <em>largest</em> single balance is answered and the
    /// currency is not: a condition written as "my balance is above 100" is a member asking whether
    /// they have a hundred of something, and the alternative — refusing to answer at all — makes the
    /// condition unusable for every member who has ever been paid in two currencies.
    /// </para>
    /// <para>
    /// In major units, invariant culture, matching how the automation evaluator parses a money
    /// operand and how the client's money editor produces one.
    /// </para>
    /// </summary>
    private async Task ReadBalanceAsync(
        AutomationConditionQuery request,
        Dictionary<string, string?> answers,
        CancellationToken cancellationToken)
    {
        var wallets = await _wallets.ListByOwnerAsync(request.Member.Value, cancellationToken);

        var spendable = wallets
            .Where(wallet => wallet.WalletType == WalletType.Primary)
            .Where(wallet => wallet.Status == WalletStatus.Active)
            .Select(wallet => wallet.Available.Amount)
            .ToArray();

        if (spendable.Length == 0)
        {
            // No wallet at all. The key is omitted rather than answered zero: "you have no wallet" and
            // "your balance is nothing" are different, and a member with no wallet who wrote "balance
            // below 10" should not have the rule fire.
            _logger.LogDebug(
                "No active primary wallet for the automation owner; walletBalance is unanswered.");

            return;
        }

        answers[AutomationConditionKeys.WalletBalance] =
            spendable.Max().ToString(CultureInfo.InvariantCulture);
    }
}
