namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// What a gateway said.
/// <para>
/// Five outcomes rather than a boolean, because the four failure-shaped ones need different
/// handling: a declined card is the payer's problem, an unavailable gateway is ours, a retryable
/// error should be retried and a challenge is not a failure at all.
/// </para>
/// </summary>
public enum GatewayOutcome
{
    /// <summary>The operation succeeded.</summary>
    Succeeded = 1,

    /// <summary>The issuer wants the payer to do something. Not a failure.</summary>
    RequiresAction = 2,

    /// <summary>The issuer refused. Retrying the same instrument will refuse again.</summary>
    Declined = 3,

    /// <summary>Something went wrong that might not next time.</summary>
    Retryable = 4,

    /// <summary>The gateway could not be reached or refused to answer.</summary>
    Unavailable = 5
}
