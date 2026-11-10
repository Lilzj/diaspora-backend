namespace Diaspora.Platform.Operations.Scalability;

/// <summary>
/// How many copies of this application the deployment runs, declared once where the platform can
/// read it.
/// <para>
/// <b>Nothing can detect this at runtime.</b> A process cannot count its own replicas, so a platform
/// that guessed would guess one and be silently wrong on every scaled deployment. The number is an
/// operator's statement, and the only thing that makes a statement load-bearing is something that
/// refuses to start when it is not true — which is
/// <c>Diaspora.Platform.Validation.Validators.DeploymentTopologyValidator</c>.
/// </para>
/// <para>
/// <b>Why this is platform-owned rather than a module's setting.</b> It used to live at
/// <c>Notifications:Realtime:InstanceCount</c>, where exactly one validator read it and only to
/// decide whether real-time delivery needed a backplane. Three other components depend on the same
/// fact — the in-memory rate limiter, the background sweeps, and the outbox dispatcher's
/// concurrency — and none of them could see a Notifications option. A platform-wide constraint
/// keyed on one vertical's configuration section is how a deployment ends up half-protected: the
/// one component that checked refuses to boot, the three that did not quietly do the wrong thing.
/// The Notifications key is still read, and the validator refuses a deployment where the two
/// disagree, so the duplication is loud rather than silent until it is removed.
/// </para>
/// <para>
/// Deliberately not a count of anything observable: see
/// <see cref="IRegionContext"/> for the same shape applied to regions, which is also a declaration
/// rather than a discovery.
/// </para>
/// </summary>
public sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";

    /// <summary>
    /// How many copies of this application serve traffic. One unless the deployment says otherwise,
    /// because one is what the shipped platform supports and a default that assumed more would be a
    /// guess in the dangerous direction.
    /// </summary>
    public int InstanceCount { get; set; } = 1;
}
