using Diaspora.Platform.Operations.Dashboards;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Dashboards;

/// <summary>
/// Assembles a board from whichever contributors name it.
/// <para>
/// Contributors run concurrently and one that throws becomes a panel that says so. A dashboard is
/// consulted during incidents; a dashboard that fails to render because one module's panel query
/// errored is a dashboard that is unavailable exactly when it is wanted, and the panel that failed is
/// often the one pointing at the problem.
/// </para>
/// </summary>
public sealed class OperationalDashboardService : IOperationalDashboardService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IReadOnlyList<string> _dashboards;
    private readonly IClock _clock;
    private readonly ILogger<OperationalDashboardService> _logger;
    private readonly string _instance;

    /// <summary>
    /// Takes a scope factory, for the reason the health aggregator does: a module's dashboard panel is
    /// numbers about its own data, which means a <c>DbContext</c>, which means a scoped contributor. A
    /// singleton that captured one would hold a disposed context after the first board was rendered.
    /// <para>
    /// The contributor list is read once at construction — from a scope opened and closed immediately —
    /// only to answer <see cref="Available"/> and to report a mis-named board at startup rather than
    /// on the first request for it.
    /// </para>
    /// </summary>
    public OperationalDashboardService(
        IServiceScopeFactory scopes,
        IClock clock,
        ILogger<OperationalDashboardService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _scopes = scopes;
        _clock = clock;
        _logger = logger;
        _instance = $"{Environment.MachineName}/{Environment.ProcessId}";

        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider.GetServices<IDashboardContributor>().ToArray();

        _dashboards =
            [.. contributors
                .Select(contributor => contributor.Dashboard)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)];

        foreach (var contributor in contributors.Where(
            candidate => !DashboardNames.All.Contains(candidate.Dashboard, StringComparer.Ordinal)))
        {
            // Not fatal. A module naming a board the platform does not declare gets its own board
            // rather than being silently dropped — but it is said out loud, because the usual cause is
            // a typo and the symptom is a panel that never appears anywhere.
            _logger.LogWarning(
                "{Module} contributes to dashboard '{Dashboard}', which the platform does not declare. " +
                "It will be served under that name; check for a typo against DashboardNames.",
                contributor.Module, contributor.Dashboard);
        }
    }

    public IReadOnlyList<string> Available => _dashboards;

    public async Task<OperationalDashboard?> BuildAsync(
        string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider
            .GetServices<IDashboardContributor>()
            .Where(contributor => string.Equals(contributor.Dashboard, name, StringComparison.Ordinal))
            .ToArray();

        if (contributors.Length == 0)
        {
            return null;
        }

        var panels = await Task.WhenAll(contributors.Select(contributor => BuildAsync(contributor, cancellationToken)))
            .ConfigureAwait(false);

        return new OperationalDashboard(
            name,
            DashboardNames.TitleOf(name),
            _clock.UtcNow,
            _instance,
            [.. panels.SelectMany(panel => panel)]);
    }

    private async Task<IReadOnlyList<DashboardPanel>> BuildAsync(
        IDashboardContributor contributor, CancellationToken cancellationToken)
    {
        try
        {
            return await contributor.BuildAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "The {Module} contributor to the {Dashboard} dashboard threw.",
                contributor.Module, contributor.Dashboard);

            return
            [
                new DashboardPanel(
                    $"{contributor.Module} (unavailable)",
                    [new DashboardMetric("Error", exception.Message, DashboardStatus.Unknown)],
                    contributor.Module,
                    "This panel failed to build. The rest of the board is unaffected.")
            ];
        }
    }
}
