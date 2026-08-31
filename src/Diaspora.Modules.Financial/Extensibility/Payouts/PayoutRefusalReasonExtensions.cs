namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// Turns a routing refusal into the code a client sees.
/// <para>
/// <b>An exhaustive switch rather than a dictionary, so adding a reason without a code does not
/// compile.</b> The failure this guards against is a new refusal reason silently mapping to the
/// generic <c>financial.payout.not_supported</c>, which would undo the reason the reasons exist.
/// </para>
/// </summary>
public static class PayoutRefusalReasonExtensions
{
    public static string ToErrorCode(this PayoutRefusalReason reason) => reason switch
    {
        PayoutRefusalReason.CountryUnsupported => FinancialErrorCodes.PayoutCountryUnsupported,
        PayoutRefusalReason.MethodUnsupported => FinancialErrorCodes.PayoutMethodUnsupported,
        PayoutRefusalReason.CurrencyUnsupported => FinancialErrorCodes.PayoutCurrencyUnsupported,
        PayoutRefusalReason.CapabilityUnknown => FinancialErrorCodes.PayoutCapabilityUnknown,
        PayoutRefusalReason.RouteDisabled => FinancialErrorCodes.PayoutRouteDisabled,
        PayoutRefusalReason.AmountOutOfRange => FinancialErrorCodes.PayoutAmountOutOfRange,
        PayoutRefusalReason.PayeeVerificationRequired => FinancialErrorCodes.PayoutVerificationRequired,
        PayoutRefusalReason.ProviderNotRegistered => FinancialErrorCodes.PayoutProviderNotRegistered,
        PayoutRefusalReason.ProviderOnboardingRequired => FinancialErrorCodes.PayoutProviderOnboardingRequired,
        PayoutRefusalReason.DestinationProviderUnavailable => FinancialErrorCodes.PayoutDestinationProviderUnavailable,

        // None means a provider was chosen. Asking a routed result for its failure code is a caller
        // bug rather than a state, so it throws instead of inventing a code.
        PayoutRefusalReason.None => throw new InvalidOperationException(
            "A routed result has no error code. Check IsRouted before mapping a refusal."),

        _ => throw new ArgumentOutOfRangeException(
            nameof(reason), reason, "No error code is mapped for that refusal reason.")
    };
}
