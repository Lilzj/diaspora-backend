using System.Globalization;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A community contribution completed — receipted to the contributor.
/// <para>
/// <b>Three of B15's five contribution kinds are not money</b>, so this is the one receipt on the
/// platform whose amount may be "12 hours" rather than a sum. The contract carries either a monetary
/// amount or a pledge quantity and unit, and never both; the template's variable is therefore plain
/// text rather than a currency, because a currency format would have to invent a currency for the
/// pledges that have none.
/// </para>
/// <para>
/// Anonymity is honoured exactly as it is for a donation, and for the same reason.
/// </para>
/// </summary>
public sealed class CommunityContributionCompletedConsumer
    : ModuleEventConsumer<CommunityContributionCompletedIntegrationEvent>
{
    public CommunityContributionCompletedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityContributionCompletedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.IsAnonymous || integrationEvent.ContributorUserId is not { } contributorUserId)
        {
            yield break;
        }

        var values = Values();
        values["contribution"] = Describe(integrationEvent);
        values["fundCode"] = integrationEvent.FundCode;
        values["kind"] = integrationEvent.Kind;
        values["receiptReference"] = integrationEvent.ReceiptReference;
        values["contributionId"] = integrationEvent.ContributionId;

        yield return Trigger(
            UserId.From(contributorUserId),
            "contributor",
            integrationEvent.CompletedAt,
            values,
            subjectType: "contribution",
            subjectId: integrationEvent.ContributionId.ToString());
    }

    /// <summary>
    /// What was given, in the terms it was given in.
    /// <para>
    /// Money first, because a monetary contribution carries both fields on some paths and the sum is
    /// the more precise of the two. Falls back to the kind alone when a contribution carries neither
    /// — a pledge of a skill has no quantity, and "your contribution of Skill is recorded" is worse
    /// than clumsy but much better than a receipt with a blank in it.
    /// </para>
    /// </summary>
    private static string Describe(CommunityContributionCompletedIntegrationEvent integrationEvent)
    {
        if (integrationEvent is { AmountMinorUnits: { } minorUnits, Currency: { } currency }
            && !string.IsNullOrWhiteSpace(currency))
        {
            return FinancialConsumerConstants.Money(minorUnits, currency);
        }

        if (integrationEvent.PledgeQuantity is { } quantity)
        {
            var unit = integrationEvent.PledgeUnit;

            return string.IsNullOrWhiteSpace(unit)
                ? quantity.ToString("0.##", CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{quantity:0.##} {unit.Trim()}");
        }

        return integrationEvent.Kind;
    }
}
