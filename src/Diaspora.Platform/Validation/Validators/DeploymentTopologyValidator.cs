using System.Globalization;
using Diaspora.Platform.Operations.Scalability;
using Diaspora.Platform.Operations.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Validation.Validators;

/// <summary>
/// Refuses to start a deployment that claims more instances than the platform can serve correctly.
/// <para>
/// <b>The platform is single-instance by construction, and this is the only place that says so
/// where it costs something to be wrong.</b> Four components assume exactly one copy: the rate
/// limiter partitions in memory, the background sweeps coordinate through a lock that is
/// in-process, the outbox dispatcher is sized for one dispatcher, and real-time delivery reaches
/// only the members attached to the instance that produced it. Before this validator the
/// assumption was recorded in an <c>appsettings.json</c> comment, an XML summary on
/// <c>RateLimitingRegistration</c>, and one module's validator that checked the real-time case
/// alone — three statements in three places, none of which an operator reads before running
/// <c>fly scale count 3</c>.
/// </para>
/// <para>
/// <b>Why refusing to boot is the right failure.</b> Every one of these components fails by doing
/// something quietly reasonable. Three instances enforce three times the configured rate limit and
/// log nothing. Three instances run every expiry, placement, withdrawal and recurring-donation
/// sweep three times, and the sweeps are idempotent enough that most of the damage is invisible
/// until a charge is duplicated. There is no symptom to alert on, so the check has to happen at the
/// one moment a human is watching: start-up.
/// </para>
/// <para>
/// <b>What it cannot check.</b> Nothing here counts machines — a process cannot see its own
/// replicas. It checks a declaration against the capabilities that declaration requires, so an
/// operator who scales the deployment without changing
/// <see cref="DeploymentOptions.InstanceCount"/> is not caught by this and never could be. That
/// gap is why <c>fly.toml</c> carries the constraint in prose as well: the file cannot enforce a
/// cap, because no Fly configuration key caps machine count.
/// </para>
/// </summary>
public sealed class DeploymentTopologyValidator : IStartupValidator
{
    /// <summary>
    /// The Notifications key that used to be the only declaration of this fact.
    /// <para>
    /// Read as a configuration path rather than through <c>RealtimeOptions</c>, because the kernel
    /// must not reference a module. That is the same reason the duplication exists at all, and
    /// comparing the two values is what stops it drifting while it does.
    /// </para>
    /// </summary>
    private const string RealtimeInstanceCountKey = "Notifications:Realtime:InstanceCount";

    public string Name => nameof(DeploymentTopologyValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deployment = context.Services.GetService<IOptions<DeploymentOptions>>()?.Value
            ?? new DeploymentOptions();

        if (deployment.InstanceCount < 1)
        {
            yield return StartupFinding.Error(
                "SV8001",
                $"{DeploymentOptions.SectionName}:InstanceCount",
                $"InstanceCount is {deployment.InstanceCount.ToString(CultureInfo.InvariantCulture)}. "
                + "It states how many copies of this application serve traffic, which is what "
                + "decides whether distributed coordination is required, and a deployment serving "
                + "no copies is not a deployment. Set it to at least 1.");

            // Everything below reasons about how many instances there are. With a nonsensical
            // count they would report on a topology nobody configured, which is noise on a boot
            // log an operator is already reading because the host refused to start.
            yield break;
        }

        foreach (var finding in ValidateMultiInstanceCapabilities(context, deployment))
        {
            yield return finding;
        }

        foreach (var finding in ValidateTheDeclarationIsNotContradicted(context, deployment))
        {
            yield return finding;
        }
    }

    private static IEnumerable<StartupFinding> ValidateMultiInstanceCapabilities(
        StartupValidationContext context,
        DeploymentOptions deployment)
    {
        if (deployment.InstanceCount == 1)
        {
            yield break;
        }

        var instances = deployment.InstanceCount.ToString(CultureInfo.InvariantCulture);

        // Resolved rather than read from a descriptor, because "does this lock span instances" is a
        // property of the implementation and not of how it was registered — a lock supplied by a
        // factory would be invisible to a descriptor check and is exactly how a correct deployment
        // would be refused. A missing registration counts as not distributed: absent coordination
        // and in-process coordination fail the same way.
        var locks = context.Services.GetService<IDistributedLock>();

        if (locks is null || !locks.IsDistributed)
        {
            yield return StartupFinding.Error(
                "SV8002",
                $"{DeploymentOptions.SectionName}:InstanceCount",
                $"This deployment declares {instances} instances and the registered "
                + $"{nameof(IDistributedLock)} does not span them, so every timer-driven sweep runs "
                + "on every instance: expiry, placement, withdrawal, recurring donations, campaign "
                + "settlement, notification delivery, analytics projection and the rest. The sweeps "
                + "are idempotent to varying degrees and recurring donations are the one where "
                + "running twice is a second charge. Register an IDistributedLock whose "
                + "IsDistributed is true — a Postgres advisory lock needs no new infrastructure — "
                + "or set InstanceCount to 1.");
        }

        // Unconditional, because it is a fact about the shipped platform rather than about this
        // deployment: the limiter has no distributed store to configure. When one is added this
        // check gains a condition, and until then an error that cannot be cleared by configuration
        // is the honest report — the alternative is a warning an operator can scroll past on the
        // way to tripling every budget that protects credential exchange.
        yield return StartupFinding.Error(
            "SV8003",
            $"{DeploymentOptions.SectionName}:InstanceCount",
            $"This deployment declares {instances} instances and the platform's rate limiter "
            + "partitions in memory, so each instance enforces every budget independently and the "
            + $"effective limit is {instances} times the configured one — sign-in, registration, "
            + "one-time codes, recovery and refresh alike. There is no distributed store to "
            + "configure: the platform ships none, and adding one is a limiter registration rather "
            + "than a redesign. Until it exists, set InstanceCount to 1.");
    }

    private static IEnumerable<StartupFinding> ValidateTheDeclarationIsNotContradicted(
        StartupValidationContext context,
        DeploymentOptions deployment)
    {
        var configuration = context.Services.GetService<IConfiguration>();

        if (configuration is null)
        {
            yield break;
        }

        // Read as a string and parsed here rather than bound, so a value that is not a number is
        // reported as a contradiction rather than silently becoming the default and agreeing.
        var raw = configuration[RealtimeInstanceCountKey];

        // Absent means the Notifications default, which is 1. That case matters most: a deployment
        // that raised the platform's count and left this alone has a real-time runtime that still
        // believes it is alone and will not ask for a backplane.
        var realtime = 1;

        if (!string.IsNullOrWhiteSpace(raw)
            && !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out realtime))
        {
            yield return StartupFinding.Error(
                "SV8004",
                RealtimeInstanceCountKey,
                $"{RealtimeInstanceCountKey} is '{raw}', which is not a whole number. It must agree "
                + $"with {DeploymentOptions.SectionName}:InstanceCount, and a value that cannot be "
                + "read would quietly become 1 while the platform believed something else.");

            yield break;
        }

        if (realtime != deployment.InstanceCount)
        {
            yield return StartupFinding.Error(
                "SV8004",
                RealtimeInstanceCountKey,
                $"{DeploymentOptions.SectionName}:InstanceCount is "
                + $"{deployment.InstanceCount.ToString(CultureInfo.InvariantCulture)} and "
                + $"{RealtimeInstanceCountKey} is "
                + $"{realtime.ToString(CultureInfo.InvariantCulture)}. Two answers to one question "
                + "means one of the two checks that depend on it is running on the wrong number — "
                + "the real-time runtime would not ask for a backplane it needs, or would ask for "
                + "one it does not. Set both to the same value.");
        }
    }
}
