namespace Diaspora.Sms;

/// <summary>
/// One vendor's wire. Everything above this interface is routing; everything below it is
/// somebody else's HTTP API.
/// </summary>
public interface ISmsGateway
{
    /// <summary>The <see cref="SmsProviderNames"/> value this gateway answers to.</summary>
    string Provider { get; }

    /// <summary>
    /// Whether this deployment gave it enough to work with. An unconfigured gateway is skipped
    /// by the router rather than called and failed, so a half-filled configuration section
    /// produces a startup message naming the missing key instead of a 401 on somebody's sign-in.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Sends one message, or throws.
    /// <para>
    /// <b>Throwing is the contract, not an accident.</b> The platform's retry lives in
    /// <c>IResilienceExecutor</c>, which decides by classifying an exception — so a gateway that
    /// returned a failure value would be retried zero times and reported as a success. The two
    /// exception types below are what makes the difference between "try again" and "stop".
    /// </para>
    /// </summary>
    /// <exception cref="SmsTransientException">The message may still get through. Retry it.</exception>
    /// <exception cref="SmsPermanentException">It will not. Retrying spends money to learn that twice.</exception>
    Task<SmsDispatch> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}

/// <summary>What to send, and where.</summary>
/// <param name="Destination">E.164, with the leading <c>+</c>.</param>
/// <param name="Body">The rendered text. Already localised; a gateway never composes copy.</param>
public sealed record SmsMessage(string Destination, string Body);

/// <summary>An accepted message.</summary>
/// <param name="Provider">Which wire carried it — the fact an operator needs when a member says nothing arrived.</param>
/// <param name="ProviderMessageId">The vendor's own identifier, for reconciling against their console. Null if they returned none.</param>
public sealed record SmsDispatch(string Provider, string? ProviderMessageId);

/// <summary>A failure from a gateway.</summary>
public abstract class SmsGatewayException : Exception
{
    protected SmsGatewayException(string provider, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Provider = provider;
    }

    public string Provider { get; }
}

/// <summary>
/// Worth another attempt: a timeout, a reset connection, a 5xx, a rate limit.
/// </summary>
public sealed class SmsTransientException : SmsGatewayException
{
    public SmsTransientException(string provider, string message, Exception? innerException = null)
        : base(provider, message, innerException)
    {
    }
}

/// <summary>
/// Not worth another attempt: an unroutable number, a rejected sender id, a bad credential, an
/// empty balance.
/// <para>
/// A bad credential is deliberately in this list. It looks like something that might heal, and
/// it never does — retrying it three times per send turns one wrong key into a rate-limit ban
/// and an open circuit on a provider that is working perfectly for everybody else.
/// </para>
/// </summary>
public sealed class SmsPermanentException : SmsGatewayException
{
    public SmsPermanentException(string provider, string message, Exception? innerException = null)
        : base(provider, message, innerException)
    {
    }
}
