namespace Diaspora.Platform.Operations.Health;

/// <summary>
/// What a health check is reporting on.
/// <para>
/// The dimension decides how a result is treated, and getting that wrong is the classic health-check
/// mistake: a liveness probe that depends on the database triggers a restart storm during a database
/// outage, and a restart storm makes the outage worse. Naming the dimension explicitly is what stops
/// a new check being wired into the wrong probe by accident.
/// </para>
/// </summary>
public enum HealthDimension
{
    /// <summary>
    /// Did this instance compose correctly? Answered once, at boot, and never changes.
    /// A failure here means the instance can never work — a bad catalogue, a missing seeder.
    /// </summary>
    Startup = 0,

    /// <summary>
    /// Should this process be killed and replaced? Must depend on nothing external, ever.
    /// </summary>
    Liveness = 1,

    /// <summary>Should this instance receive traffic right now?</summary>
    Readiness = 2,

    /// <summary>A dependency this instance needs.</summary>
    Dependency = 3,

    /// <summary>The database.</summary>
    Storage = 4,

    /// <summary>The cache.</summary>
    Cache = 5,

    /// <summary>The outbox and the transport.</summary>
    Messaging = 6,

    /// <summary>Search.</summary>
    Search = 7,

    /// <summary>The financial platform's own invariants — the ledger, the gateways.</summary>
    Financial = 8,

    /// <summary>Object storage and the media pipeline.</summary>
    Media = 9,

    /// <summary>A third-party provider.</summary>
    ExternalProvider = 10,

    /// <summary>Background workers.</summary>
    BackgroundWorker = 11
}

/// <summary>A health verdict. Ordered, so the worst of a set is the maximum.</summary>
public enum PlatformHealthState
{
    Healthy = 0,

    /// <summary>Working, with something impaired. The instance keeps traffic.</summary>
    Degraded = 1,

    /// <summary>Not working. A critical check in this state takes the instance out of rotation.</summary>
    Unhealthy = 2
}

/// <summary>
/// One check's answer.
/// </summary>
/// <param name="Name">Unique check name.</param>
/// <param name="Module">Who owns it.</param>
/// <param name="Dimension">What it reports on.</param>
/// <param name="State">The verdict.</param>
/// <param name="Description">One line a human reads during an incident.</param>
/// <param name="Duration">How long the check took.</param>
/// <param name="Data">
/// Operational counts, never a payload, an identifier or anything a member wrote. The rule B1.1
/// established for the outbox check and the reason every check's data travels to the endpoint: a
/// per-check allowlist would go stale the first time somebody added a check and forgot.
/// </param>
/// <param name="Error">An exception message, never a stack trace.</param>
public sealed record ModuleHealthResult(
    string Name,
    string Module,
    HealthDimension Dimension,
    PlatformHealthState State,
    string Description,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object>? Data = null,
    string? Error = null)
{
    public static ModuleHealthResult Healthy(
        string name, string module, HealthDimension dimension, string description,
        IReadOnlyDictionary<string, object>? data = null) =>
        new(name, module, dimension, PlatformHealthState.Healthy, description, TimeSpan.Zero, data);

    public static ModuleHealthResult Degraded(
        string name, string module, HealthDimension dimension, string description,
        IReadOnlyDictionary<string, object>? data = null) =>
        new(name, module, dimension, PlatformHealthState.Degraded, description, TimeSpan.Zero, data);

    public static ModuleHealthResult Unhealthy(
        string name, string module, HealthDimension dimension, string description,
        IReadOnlyDictionary<string, object>? data = null, string? error = null) =>
        new(name, module, dimension, PlatformHealthState.Unhealthy, description, TimeSpan.Zero, data, error);
}

/// <summary>
/// A module's health, reported in the platform's own vocabulary.
/// <para>
/// Provider-neutral on purpose. ASP.NET Core's <c>IHealthCheck</c> is the transport, and the host
/// adapts these onto it — but a module that implemented <c>IHealthCheck</c> directly would be a module
/// that could not be health-checked in a worker process, and would have to know about the tag strings
/// the readiness endpoint filters on.
/// </para>
/// <para>
/// <b>A contributor must not throw.</b> An exception is caught by the aggregator and reported as
/// unhealthy with the message, because a health system that can be taken down by a health check is
/// worse than no health system.
/// </para>
/// </summary>
public interface IModuleHealthContributor
{
    /// <summary>The owning module.</summary>
    string Module { get; }

    /// <summary>
    /// Runs this module's checks.
    /// <para>
    /// One call per module rather than one per check, so a module that needs one connection to answer
    /// five questions opens one connection.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ModuleHealthResult>> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The whole platform's health, as one instance sees it.
/// </summary>
/// <param name="State">The aggregate: the worst critical result, or the worst result if none is critical.</param>
/// <param name="Instance">Which instance answered. Health is per-instance and saying so avoids a whole class of confusion.</param>
/// <param name="CheckedAt">When.</param>
/// <param name="TotalDuration">How long the whole pass took.</param>
/// <param name="Results">Every check.</param>
/// <param name="ByDimension">The worst state per dimension, which is what a dashboard renders.</param>
public sealed record PlatformHealthModel(
    PlatformHealthState State,
    string Instance,
    DateTimeOffset CheckedAt,
    TimeSpan TotalDuration,
    IReadOnlyList<ModuleHealthResult> Results,
    IReadOnlyDictionary<HealthDimension, PlatformHealthState> ByDimension);

/// <summary>Runs every contributor and aggregates the answers.</summary>
public interface IPlatformHealthAggregator
{
    /// <summary>Runs the checks for a dimension, or all of them.</summary>
    Task<PlatformHealthModel> CheckAsync(
        HealthDimension? dimension = null, CancellationToken cancellationToken = default);
}
