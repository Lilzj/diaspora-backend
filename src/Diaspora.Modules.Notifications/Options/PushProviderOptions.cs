namespace Diaspora.Modules.Notifications.Options;

/// <summary>
/// How the push transport is configured, without naming one.
/// <para>
/// <b>There is no <c>FirebaseOptions</c> in this platform, and there is not going to be.</b> An
/// architecture test refuses any type in this module whose name contains a vendor, and this class is
/// why that rule survives contact with configuration: the vendor is a <em>value</em> of
/// <see cref="Provider"/>, not a type, a section name or a property. A deployment that pushes
/// through Firebase Cloud Messaging sets <c>Provider: "Firebase"</c> and fills in the same four
/// fields any provider needs — a project, a credential, an endpoint and a timeout. One that moves to
/// APNs directly changes the string.
/// </para>
/// <para>
/// <b>No credential ships in this repository, and none of these fields has a non-empty default.</b>
/// <see cref="CredentialsPath"/> points at a file a deployment mounts; <see cref="CredentialsJson"/>
/// exists for a secret store that injects by environment variable and is refused outside Development
/// when a path is also given, because two sources for one credential is how a deployment ends up
/// authenticating as last year's service account.
/// </para>
/// <para>
/// <b>Setting <see cref="Provider"/> does not make push work.</b> It states an intention; an adapter
/// registered against <c>IPushNotificationSender</c> is what fulfils it. Naming a provider with no
/// adapter registered is a startup <em>error</em> rather than a quiet fallback to sending nothing —
/// see <c>CommunicationRuntimeValidator</c>. That distinction is the whole point of the setting:
/// today's silence is a decision, and tomorrow's silence must be an alarm.
/// </para>
/// </summary>
public sealed class PushProviderOptions
{
    public const string SectionName = "Notifications:Push";

    /// <summary>
    /// The name nothing is configured with. The shipped default.
    /// <para>
    /// Explicit rather than an empty string, so a configuration file states the position rather than
    /// omitting it — "we have not chosen a push provider" and "somebody deleted this line" should
    /// not look the same.
    /// </para>
    /// </summary>
    public const string NoProvider = "None";

    /// <summary>
    /// The in-process provider that reports success and sends nothing.
    /// <para>
    /// For a development or staging deployment that needs the <em>configured</em> path exercised —
    /// delivery rows reaching <c>Delivered</c>, analytics moving, endpoints having their failure
    /// counts cleared — without a vendor account. Refused outside Development and Testing by the
    /// startup validator, because a production deployment silently marking every push delivered is
    /// worse than one honestly reporting that nothing is configured.
    /// </para>
    /// </summary>
    public const string LoopbackProvider = "Loopback";

    /// <summary>
    /// Which transport to use. <see cref="NoProvider"/>, <see cref="LoopbackProvider"/>, or the name
    /// of whatever adapter the deployment registered.
    /// </summary>
    public string Provider { get; set; } = NoProvider;

    /// <summary>
    /// The provider-side project, application or team identifier. Not a secret.
    /// <para>
    /// Named generically because every provider has one under a different word — a project for one,
    /// an application for another, a team and a bundle for a third — and the platform's job is to
    /// carry it, not to spell it.
    /// </para>
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Path to a mounted credential file. The way a deployment should supply one.
    /// <para>
    /// A path rather than the contents, so the secret lives in the orchestrator's secret store and
    /// never in configuration, in an image layer, or in a log line that dumped the bound options.
    /// </para>
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// The credential itself, for a secret store that injects by environment variable.
    /// <para>
    /// Supported because some platforms genuinely have no filesystem to mount into, and refused
    /// alongside <see cref="CredentialsPath"/> because two sources for one credential is a deployment
    /// that cannot say which one it is using.
    /// </para>
    /// </summary>
    public string? CredentialsJson { get; set; }

    /// <summary>The provider's endpoint, when a deployment must pin one. Null means the adapter's own default.</summary>
    public string? Endpoint { get; set; }

    /// <summary>How long to wait on the provider before treating the attempt as transient.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Ask the provider to validate without delivering, where it supports that.
    /// <para>
    /// The safe way to prove a credential in a new environment: the whole path runs, the provider
    /// authenticates and answers, and nobody's phone lights up.
    /// </para>
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Endpoints one provider call may carry.
    /// <para>
    /// Five hundred, which is the smallest multicast limit among the providers anybody is likely to
    /// pick. An adapter that can carry more raises it; one that can carry fewer must lower it, and
    /// having the number here rather than inside an adapter is what lets an operator do that without
    /// a release.
    /// </para>
    /// </summary>
    public int MaxEndpointsPerRequest { get; set; } = 500;

    /// <summary>
    /// Anything else an adapter needs, as free key/value pairs.
    /// <para>
    /// The escape hatch that stops the next provider from being a change to this class. An adapter
    /// reads the keys it knows and ignores the rest; nothing in this module reads any of them, and
    /// nothing here should — the moment this module understood a key, it would understand a vendor.
    /// </para>
    /// </summary>
    public IDictionary<string, string> Settings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the deployment has named any provider at all.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Provider)
        && !string.Equals(Provider, NoProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the named provider is the in-process one that sends nothing.</summary>
    public bool IsLoopback =>
        string.Equals(Provider, LoopbackProvider, StringComparison.OrdinalIgnoreCase);
}
