namespace Diaspora.Platform.Operations.Dashboards;

/// <summary>How a value should read to somebody scanning a board.</summary>
public enum DashboardStatus
{
    /// <summary>A number with no judgement attached.</summary>
    Neutral = 0,

    /// <summary>Within expectations.</summary>
    Good = 1,

    /// <summary>Worth watching.</summary>
    Warning = 2,

    /// <summary>Wrong now.</summary>
    Critical = 3,

    /// <summary>
    /// Not configured or not measurable. Distinct from zero, and the distinction matters more than
    /// any other on these boards: "no backup provider is configured" and "zero backups exist" are the
    /// same number and completely different situations.
    /// </summary>
    Unknown = 4
}

/// <summary>One number, with enough around it to be read without a runbook.</summary>
/// <param name="Label">What it is.</param>
/// <param name="Value">The value, already formatted.</param>
/// <param name="Status">How it reads.</param>
/// <param name="Unit">Its unit, when a bare number would be ambiguous.</param>
/// <param name="Hint">
/// What to do about it, when it is not good. The difference between a dashboard and a wall of numbers.
/// </param>
public sealed record DashboardMetric(
    string Label,
    string Value,
    DashboardStatus Status = DashboardStatus.Neutral,
    string? Unit = null,
    string? Hint = null);

/// <summary>A group of related numbers.</summary>
/// <param name="Title">What the group is about.</param>
/// <param name="Metrics">The numbers.</param>
/// <param name="Module">Which module contributed it.</param>
/// <param name="Note">Context that applies to the whole panel.</param>
public sealed record DashboardPanel(
    string Title,
    IReadOnlyList<DashboardMetric> Metrics,
    string Module,
    string? Note = null)
{
    /// <summary>The panel's overall reading: the worst of its metrics.</summary>
    public DashboardStatus Status =>
        Metrics.Count == 0 ? DashboardStatus.Unknown : Metrics.Max(metric => metric.Status);
}

/// <summary>A whole board.</summary>
/// <param name="Name">The board's key.</param>
/// <param name="Title">Its human title.</param>
/// <param name="GeneratedAt">When it was built.</param>
/// <param name="Instance">Which instance built it. Most of these numbers are per-instance.</param>
/// <param name="Panels">Its panels, in contributor order.</param>
public sealed record OperationalDashboard(
    string Name,
    string Title,
    DateTimeOffset GeneratedAt,
    string Instance,
    IReadOnlyList<DashboardPanel> Panels)
{
    public DashboardStatus Status =>
        Panels.Count == 0 ? DashboardStatus.Unknown : Panels.Max(panel => panel.Status);
}

/// <summary>
/// The boards the platform ships.
/// <para>
/// <b>Backend projections, and no vendor's dashboard format anywhere.</b> What is produced here is the
/// data a board needs, shaped and labelled; rendering it is somebody else's job, and which somebody
/// depends on a deployment the platform has deliberately not chosen. Emitting a Grafana JSON model
/// would tie the platform's operational surface to one tool and would still not work for anyone using
/// a different one.
/// </para>
/// </summary>
public static class DashboardNames
{
    public const string PlatformHealth = "platform-health";
    public const string Performance = "performance";
    public const string Messaging = "messaging";
    public const string Queues = "queues";
    public const string Cache = "cache";
    public const string Workers = "workers";
    public const string Search = "search";
    public const string Financial = "financial";
    public const string Recommendation = "recommendation";
    public const string Communication = "communication";
    public const string Trust = "trust";
    public const string Media = "media";

    /// <summary>Every board the platform declares. A contributor naming something else is reported.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        PlatformHealth, Performance, Messaging, Queues, Cache, Workers,
        Search, Financial, Recommendation, Communication, Trust, Media
    ];

    /// <summary>The human title for a board.</summary>
    public static string TitleOf(string name) => name switch
    {
        PlatformHealth => "Platform Health",
        Performance => "Performance",
        Messaging => "Messaging & Outbox",
        Queues => "Queues",
        Cache => "Cache",
        Workers => "Background Workers",
        Search => "Search",
        Financial => "Financial Platform",
        Recommendation => "Recommendation",
        Communication => "Communication",
        Trust => "Trust",
        Media => "Media",
        _ => name
    };
}

/// <summary>
/// Contributes panels to a board.
/// <para>
/// The seam that lets the Financial Platform put its own numbers on the financial board without this
/// assembly referencing it, and without the board's shape being decided by whoever wrote it first.
/// </para>
/// <para>
/// <b>A contributor must be cheap and must not throw.</b> Boards are refreshed on a human's cadence, but
/// a panel that runs an unbounded query turns an operational dashboard into a source of load during
/// exactly the incident it exists to help with. One that throws is reported as an unknown panel rather
/// than taking the board down.
/// </para>
/// </summary>
public interface IDashboardContributor
{
    /// <summary>Which board, from <see cref="DashboardNames"/>.</summary>
    string Dashboard { get; }

    /// <summary>The contributing module.</summary>
    string Module { get; }

    Task<IReadOnlyList<DashboardPanel>> BuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>Builds boards from their contributors.</summary>
public interface IOperationalDashboardService
{
    /// <summary>Boards that have at least one contributor.</summary>
    IReadOnlyList<string> Available { get; }

    /// <summary>Builds a board, or returns null when nothing contributes to it.</summary>
    Task<OperationalDashboard?> BuildAsync(string name, CancellationToken cancellationToken = default);
}
