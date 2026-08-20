using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Infrastructure.Channels;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Validation;

/// <summary>
/// Refuses to start a communication runtime that is configured to be silently wrong.
/// <para>
/// <b>The first module-owned startup validator in the platform, and the seam working as B1.4
/// intended:</b> discovered by the same scan that finds the kernel's six, registered by nothing,
/// named in no list. What it checks is the class of mistake that has no other detector — real-time
/// and push both fail by doing nothing, to one member at a time, with no exception and no log line
/// above Debug. A deployment can run for weeks scaled to three instances with no backplane, and the
/// only symptom is that two thirds of members quietly stop receiving anything.
/// </para>
/// <para>
/// <b>Error where a member would be harmed, Warning where an operator would be surprised.</b> A
/// named push provider with no adapter behind it is an error: somebody set out to send push and the
/// deployment will not, which is a promise the platform cannot keep. A real-time runtime switched
/// off entirely is a warning: it is a legitimate configuration and everything still lands in the
/// inbox.
/// </para>
/// <para>
/// Runs before migrations, before the sweeps, before the first request. It reads options and service
/// descriptors and touches no database, which is what <see cref="IStartupValidator"/> requires.
/// </para>
/// </summary>
public sealed class CommunicationRuntimeValidator : Platform.Validation.IStartupValidator
{
    public string Name => nameof(CommunicationRuntimeValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var realtime = context.Services.GetService<IOptions<RealtimeOptions>>()?.Value
            ?? new RealtimeOptions();

        var push = context.Services.GetService<IOptions<PushProviderOptions>>()?.Value
            ?? new PushProviderOptions();

        var environment = context.Services.GetService<IHostEnvironment>();

        foreach (var finding in ValidateRealtime(realtime))
        {
            yield return finding;
        }

        foreach (var finding in ValidatePush(push, context, environment))
        {
            yield return finding;
        }
    }

    private static IEnumerable<StartupFinding> ValidateRealtime(RealtimeOptions options)
    {
        if (!options.Enabled)
        {
            yield return StartupFinding.Warning(
                "SV7001",
                RealtimeOptions.SectionName,
                "The real-time runtime is switched off. Notifications still reach the inbox and "
                + "push, and connected clients will not be told about them until they poll. Set "
                + "Notifications:Realtime:Enabled to true to restore live delivery.");

            // Every check below is about how real-time behaves. With it off they would all report on
            // a subsystem that is not running, which is noise on a boot log an operator is reading
            // because something else went wrong.
            yield break;
        }

        // The finding this validator exists for. Nothing at runtime can count its own replicas, so a
        // platform that guessed would guess one and be silently wrong on every scaled deployment.
        if (options.InstanceCount > 1 && !IsDistributed(options))
        {
            yield return StartupFinding.Error(
                "SV7002",
                $"{RealtimeOptions.SectionName}:Backplane",
                $"This deployment declares {options.InstanceCount} instances and no real-time "
                + "backplane, so a notification is only delivered live to the members who happen to "
                + "be attached to the instance that produced it. Install a backplane adapter and set "
                + "Notifications:Realtime:Backplane:Provider, or set InstanceCount to 1.");
        }

        if (options.InstanceCount < 1)
        {
            yield return StartupFinding.Error(
                "SV7003",
                $"{RealtimeOptions.SectionName}:InstanceCount",
                "InstanceCount must be at least 1. It states how many copies of this application "
                + "the deployment runs, which is what decides whether a backplane is required.");
        }

        // Twice the keep-alive is the documented relationship. Below it, a healthy client on a slow
        // network is disconnected between pings and reconnects forever — which reads as flapping
        // presence and a rising connection count, and looks nothing like a timeout that is too short.
        if (options.ClientTimeoutSeconds < options.KeepAliveSeconds * 2)
        {
            yield return StartupFinding.Warning(
                "SV7004",
                $"{RealtimeOptions.SectionName}:ClientTimeoutSeconds",
                $"ClientTimeoutSeconds ({options.ClientTimeoutSeconds}) should be at least twice "
                + $"KeepAliveSeconds ({options.KeepAliveSeconds}). Below that, healthy clients are "
                + "disconnected between keep-alives and reconnect in a loop.");
        }

        if (options.MaxConnectionsPerUser < 1)
        {
            yield return StartupFinding.Error(
                "SV7005",
                $"{RealtimeOptions.SectionName}:MaxConnectionsPerUser",
                "MaxConnectionsPerUser must be at least 1, or no member can connect at all.");
        }

        if (options.Presence.Enabled && options.Presence.AwayAfterSeconds < 1)
        {
            yield return StartupFinding.Error(
                "SV7006",
                $"{RealtimeOptions.SectionName}:Presence:AwayAfterSeconds",
                "AwayAfterSeconds must be positive. Zero or less reports every connected member as "
                + "away the instant they connect.");
        }
    }

    private static IEnumerable<StartupFinding> ValidatePush(
        PushProviderOptions options,
        StartupValidationContext context,
        IHostEnvironment? environment)
    {
        if (!options.IsConfigured)
        {
            // Not a finding. "No push provider" is the platform's shipped, documented position: the
            // whole path above the transport runs, the delivery is recorded Unsupported, and nobody
            // is misled. Warning about it on every boot would train operators to ignore this report.
            yield break;
        }

        if (options.IsLoopback)
        {
            if (environment is not null
                && !environment.IsDevelopment()
                && !environment.IsEnvironment("Testing"))
            {
                yield return StartupFinding.Error(
                    "SV7007",
                    $"{PushProviderOptions.SectionName}:Provider",
                    $"The loopback push provider is configured in the {environment.EnvironmentName} "
                    + "environment. It marks every push delivered without sending anything, so this "
                    + "deployment would report success while no device is ever reached. Use a real "
                    + "adapter, or set the provider to None.");
            }

            yield break;
        }

        // A named provider is a statement of intent. Something has to fulfil it, and the only thing
        // that can is an adapter registered against the port after this module — so the last
        // registration winning is exactly the check.
        if (!HasAdapter(context))
        {
            yield return StartupFinding.Error(
                "SV7008",
                $"{PushProviderOptions.SectionName}:Provider",
                $"Push provider '{options.Provider}' is configured, but no adapter is registered "
                + $"against {nameof(IPushNotificationSender)} — the shipped placeholder is still in "
                + "place, so every push will be recorded as unsupported. Register the adapter after "
                + "the Notifications module, or set the provider to None.");
        }

        if (string.IsNullOrWhiteSpace(options.CredentialsPath)
            && string.IsNullOrWhiteSpace(options.CredentialsJson))
        {
            yield return StartupFinding.Error(
                "SV7009",
                $"{PushProviderOptions.SectionName}:CredentialsPath",
                $"Push provider '{options.Provider}' is configured with no credential. Set "
                + "Notifications:Push:CredentialsPath to a mounted secret, or supply the credential "
                + "through Notifications:Push:CredentialsJson from a secret store.");
        }

        // Two sources for one credential is a deployment that cannot say which one it authenticated
        // with — and the answer differs by adapter, which is how a rotation ends up applied to the
        // file while the process keeps using an environment variable set last year.
        if (!string.IsNullOrWhiteSpace(options.CredentialsPath)
            && !string.IsNullOrWhiteSpace(options.CredentialsJson))
        {
            yield return StartupFinding.Error(
                "SV7010",
                PushProviderOptions.SectionName,
                "Both CredentialsPath and CredentialsJson are set. Supply exactly one, or the "
                + "credential in use depends on which the adapter happens to read first.");
        }

        // An inline credential outside Development is a secret sitting in whatever produced the
        // configuration — an image layer, a repository, a config map printed by a diagnostic
        // endpoint. Supported, because some platforms have no filesystem to mount into, and worth
        // saying out loud every time.
        if (!string.IsNullOrWhiteSpace(options.CredentialsJson)
            && environment is not null
            && !environment.IsDevelopment())
        {
            yield return StartupFinding.Warning(
                "SV7011",
                $"{PushProviderOptions.SectionName}:CredentialsJson",
                "A push credential is supplied inline. Confirm it reaches configuration from a "
                + "secret store rather than from an appsettings file or an image layer.");
        }

        if (string.IsNullOrWhiteSpace(options.ProjectId))
        {
            yield return StartupFinding.Warning(
                "SV7012",
                $"{PushProviderOptions.SectionName}:ProjectId",
                $"Push provider '{options.Provider}' is configured with no ProjectId. Most providers "
                + "need one to route a message to the right application.");
        }
    }

    private static bool IsDistributed(RealtimeOptions options) =>
        !string.IsNullOrWhiteSpace(options.Backplane.Provider)
        && !string.Equals(options.Backplane.Provider, "None", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether anything other than the two shipped placeholders serves the push port.
    /// <para>
    /// Read from the registration snapshot rather than by resolving the service, because resolving
    /// tells you what you got and not how it was registered — and an adapter registered as a factory
    /// would resolve to a perfectly good sender that the descriptor cannot name. A factory
    /// registration is therefore counted as an adapter: somebody wrote it deliberately, and the
    /// alternative is refusing to start a correctly configured deployment.
    /// </para>
    /// </summary>
    private static bool HasAdapter(StartupValidationContext context)
    {
        var descriptors = context.Registrations
            .Where(descriptor => descriptor.ServiceType == typeof(IPushNotificationSender))
            .ToList();

        return descriptors.Exists(descriptor =>
            descriptor.ImplementationType is null
            || (descriptor.ImplementationType != typeof(UnconfiguredPushNotificationSender)
                && descriptor.ImplementationType != typeof(LoopbackPushNotificationSender)));
    }
}
