using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// Somewhere new was added for a member's money to go (BSEC-007).
/// <para>
/// <b>This is the notice that lets a member catch a theft in progress.</b> Registering a payout
/// destination is the first half of a compromised account's payout, and the half the real member
/// would recognise as wrong — so it was also the most valuable fact on the platform to tell
/// somebody about, and nothing told them. BSEC-007's third verification criterion is this message
/// existing and committing with the registration.
/// </para>
/// <para>
/// <b>Filed under security rather than billing, which is the load-bearing routing decision here.</b>
/// Billing can be muted. Security is mandatory and the rule is Critical, so this reaches a member
/// who has turned everything else off — which is exactly the member an attacker hopes for.
/// </para>
/// </summary>
public sealed class PayoutDestinationRegisteredConsumer
    : ModuleEventConsumer<PayoutDestinationRegisteredIntegrationEvent>
{
    public PayoutDestinationRegisteredConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        PayoutDestinationRegisteredIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();

        // The masked label, which is all that ever crosses the bus. A member recognises
        // "GTBank ••3421" and cannot be helped by a destination id.
        values["label"] = integrationEvent.Label;
        values["currency"] = integrationEvent.Currency;
        values["kind"] = KindLabel(integrationEvent.Kind);

        yield return Trigger(
            UserId.From(integrationEvent.OwnerUserId),
            "owner",
            integrationEvent.RegisteredAt,
            values,

            // The destination, so two destinations added in one sitting are two notices. A member
            // who added one and an attacker who added another must not be told about one of them.
            subjectType: "payout-destination",
            subjectId: integrationEvent.DestinationId.ToString());
    }

    /// <summary>
    /// The destination kind in the member's words.
    /// </summary>
    /// <remarks>
    /// Mapped rather than passed through, because B15's enum names are <c>MobileMoney</c> and
    /// <c>BankAccount</c> — correct in the domain and wrong in a sentence a member reads at
    /// midnight. An unrecognised kind falls back to a phrase that is still a true sentence.
    /// </remarks>
    internal static string KindLabel(string kind) => kind switch
    {
        "BankAccount" => "bank account",
        "MobileMoney" => "mobile money account",
        "Card" => "card",
        "PayPal" => "PayPal account",
        _ => "payout account"
    };
}
