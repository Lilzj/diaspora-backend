using System.Net.Sockets;
using Diaspora.Modules.Notifications;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;

namespace Diaspora.Email;

/// <summary>
/// The dependency names email traffic is measured and broken on.
/// </summary>
public static class EmailDependencies
{
    public const string Resend = "email.resend";

    public static string For(string provider) => provider switch
    {
        EmailProviderNames.Resend => Resend,

        // Not a fallback to a shared name. An unrecognised provider gets its own dependency so
        // its failures stay off every other provider's breaker, and so the name in the metric
        // says plainly which configuration value nobody recognised.
        _ => $"email.{provider.ToLowerInvariant()}"
    };
}

/// <summary>
/// How hard the platform tries to deliver an email, and when it stops.
/// </summary>
/// <remarks>
/// <para>
/// Declared here rather than in the platform's own contributor: the numbers are about what an
/// email is worth and how long a caller will wait for one, and neither is the platform's
/// business.
/// </para>
/// <para>
/// <b>These are not the SMS numbers.</b> Nobody is watching a screen for an email the way they
/// watch for a sign-in code, so this can afford to be more patient — more attempts, longer
/// backoff, a longer overall budget. The SMS policy is short because a member is waiting and
/// every attempt costs money; email is cheap and asynchronous, and giving up early on a
/// recoverable blip loses a message that had no deadline.
/// </para>
/// </remarks>
public sealed class EmailResiliencePolicyContributor : IOperationalPolicyContributor
{
    /// <summary>
    /// Notifications, not "Email".
    /// </summary>
    /// <remarks>
    /// The kernel attributes policy conflicts and the platform manifest by this name and warns
    /// (SV2006) when it does not match a registered module — which "Email" would not, because
    /// this is an adapter rather than a module. Notifications is the right owner on the merits
    /// too: these numbers govern how hard the platform tries to deliver a notification, which is
    /// its behaviour. The vendor stays anonymous here, as it does everywhere above the gateway.
    /// </remarks>
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<OperationalPolicy> Policies =>
    [
        RetryFor(EmailDependencies.Resend),
        BreakerFor(EmailDependencies.Resend)
    ];

    private static RetryPolicy RetryFor(string dependency) => new(dependency)
    {
        Description = "Outbound email. More patient than SMS: nobody is watching a screen for it.",

        // Four attempts in total — the executor treats this as a retry count.
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(8),

        // A queue draining after an outage retries in lockstep without this, which is a
        // thundering herd against a provider that has only just recovered.
        JitterRatio = 0.3,

        // Generous, because the caller is a background dispatch rather than a member. It still
        // has a ceiling: an unbounded send holds a worker slot that other messages need.
        OverallTimeout = TimeSpan.FromSeconds(45)
    };

    private static CircuitBreakerPolicy BreakerFor(string dependency) => new(dependency)
    {
        Description = "The email provider. Opens on sustained failure so a queue stops burning attempts.",
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(60),
        MinimumThroughput = 5,

        // Longer than the SMS breaker's twenty seconds, and for the opposite reason. There is no
        // second wire to fall back to, so an open circuit here is a genuine rest for the provider
        // rather than a routing signal. Reopening every twenty seconds would just resume
        // hammering something that has not recovered.
        BreakDuration = TimeSpan.FromSeconds(60),
        CallTimeout = TimeSpan.FromSeconds(20)
    };
}

/// <summary>
/// Tells the executor which email failures are worth another attempt.
/// </summary>
/// <remarks>
/// Registered once per provider dependency, because <see cref="IFailureClassifier.Dependency"/>
/// names exactly one and the executor looks it up by that name. Without a registration the
/// platform default applies, which treats everything it does not recognise as permanent — so a
/// missing classifier would silently disable retry rather than fail loudly.
/// </remarks>
public sealed class EmailFailureClassifier : IFailureClassifier
{
    public EmailFailureClassifier(string dependency)
    {
        Dependency = dependency;
    }

    public string? Dependency { get; }

    public FailureClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            // The gateway already decided, having read the provider's own status. That decision
            // is better informed than anything inferable from the exception type.
            EmailTransientException => FailureClassification.Transient,
            EmailPermanentException => FailureClassification.Permanent,

            // A cancelled dispatch is not evidence about a provider.
            OperationCanceledException => FailureClassification.Cancelled,

            HttpRequestException or SocketException or TimeoutException
                => FailureClassification.Transient,

            _ => FailureClassification.Permanent
        };
    }
}
