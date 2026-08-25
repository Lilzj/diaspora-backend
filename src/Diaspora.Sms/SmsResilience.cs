using System.Net.Sockets;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;

namespace Diaspora.Sms;

/// <summary>
/// The dependency names SMS calls execute under.
/// <para>
/// One per provider, not one for "SMS". The executor keys its circuit breaker and its bulkhead
/// by this string, so a shared name would let a failing Twilio open the circuit that Africa's
/// Talking is behind — and the whole reason two providers are configured is that one of them
/// failing should not stop a code reaching a member.
/// </para>
/// </summary>
public static class SmsDependencies
{
    public const string Twilio = "sms.twilio";

    public const string AfricasTalking = "sms.africas-talking";

    /// <summary>The dependency name a provider's calls run under.</summary>
    public static string For(string provider) => provider switch
    {
        SmsProviderNames.Twilio => Twilio,
        SmsProviderNames.AfricasTalking => AfricasTalking,

        // Not a fallback to a shared name. An unrecognised provider gets its own dependency so
        // its failures stay off every other provider's breaker, and so the name in the metric
        // says plainly which configuration value nobody recognised.
        _ => $"sms.{provider.ToLowerInvariant()}"
    };
}

/// <summary>
/// Retry and breaker settings for outbound SMS.
/// <para>
/// <b>Why retrying a send at all is safe here, when retrying a payment is not.</b> The platform's
/// strictest breaker exists because a retried charge can take a member's money twice. A retried
/// one-time code cannot: <see cref="Diaspora.Application.Identity.Models.OtpDeliveryRequest"/>
/// carries a code that was generated once, so every attempt delivers the <em>same digits</em>.
/// The worst case of a duplicate is that a member receives one code twice and it still works —
/// which is why this is a retry policy and not a no-retry dependency like a non-idempotent
/// integration call.
/// </para>
/// <para>
/// It is still deliberately small. Two retries, not the platform default's three, and a fifteen
/// second overall ceiling: a member is watching a screen and will tap <em>Resend</em> long before
/// a patient backoff finishes, and every attempt after the one that worked is money.
/// </para>
/// </summary>
public sealed class SmsResiliencePolicyContributor : IOperationalPolicyContributor
{
    public string Module => "Sms";

    public IEnumerable<OperationalPolicy> Policies =>
    [
        RetryFor(SmsDependencies.Twilio),
        RetryFor(SmsDependencies.AfricasTalking),
        BreakerFor(SmsDependencies.Twilio),
        BreakerFor(SmsDependencies.AfricasTalking)
    ];

    private static RetryPolicy RetryFor(string dependency) => new(dependency)
    {
        Description = "Outbound SMS. Small on purpose: a member is waiting, and every attempt costs.",

        // Three attempts in total — the executor treats this as a retry count.
        MaxAttempts = 2,
        InitialDelay = TimeSpan.FromMilliseconds(400),
        MaxDelay = TimeSpan.FromSeconds(3),

        // Two providers retrying in lockstep after a shared upstream blip is a thundering herd
        // against whichever of them recovers first.
        JitterRatio = 0.3,

        // Under the fifteen seconds a member will wait before deciding nothing is coming.
        OverallTimeout = TimeSpan.FromSeconds(12)
    };

    private static CircuitBreakerPolicy BreakerFor(string dependency) => new(dependency)
    {
        Description = "One SMS provider. Opens fast, recovers fast, so the router can use the other one.",
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,

        // Short, because an open circuit here is not a rest for the provider — it is the signal
        // the router uses to send the next code down the other wire. Holding it open for a
        // minute would keep paying the more expensive fallback long after the cheap one healed.
        BreakDuration = TimeSpan.FromSeconds(20),
        CallTimeout = TimeSpan.FromSeconds(10)
    };
}

/// <summary>
/// Tells the executor which SMS failures are worth another attempt.
/// <para>
/// Registered once per provider dependency, because <see cref="IFailureClassifier.Dependency"/>
/// names exactly one and the executor looks it up by that name. Without a registration the
/// platform default applies, which treats everything it does not recognise as permanent — so an
/// unregistered classifier would silently disable retry rather than fail loudly.
/// </para>
/// </summary>
public sealed class SmsFailureClassifier : IFailureClassifier
{
    public SmsFailureClassifier(string dependency)
    {
        Dependency = dependency;
    }

    public string? Dependency { get; }

    public FailureClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            // The gateway already decided, having read the provider's own error code. That
            // decision is better informed than anything inferable from the exception type.
            SmsTransientException => FailureClassification.Transient,
            SmsPermanentException => FailureClassification.Permanent,

            // A member abandoning the screen is not evidence about a provider.
            OperationCanceledException => FailureClassification.Cancelled,

            HttpRequestException or SocketException or TimeoutException
                => FailureClassification.Transient,

            _ => FailureClassification.Permanent
        };
    }
}
