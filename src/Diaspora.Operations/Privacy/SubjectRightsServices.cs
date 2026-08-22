using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Privacy;

/// <summary>
/// Assembles a member's data across every registered <see cref="IDataExportProvider"/>.
/// <para>
/// <b>Scoped, and it does not open a scope of its own.</b> <see cref="PrivacyAuditor"/> does, because
/// it is a singleton that only counts providers and never touches a member's rows. These two are the
/// opposite case: they must run on the <em>caller's</em> <c>DbContext</c>, so that what a provider
/// changes is committed by the caller's unit of work.
/// </para>
/// </summary>
public sealed class PersonalDataExportService : IPersonalDataExportService
{
    private readonly ModuleRegistry _modules;
    private readonly IReadOnlyList<IDataExportProvider> _providers;
    private readonly IClock _clock;
    private readonly ILogger<PersonalDataExportService> _logger;

    public PersonalDataExportService(
        ModuleRegistry modules,
        IEnumerable<IDataExportProvider> providers,
        IClock clock,
        ILogger<PersonalDataExportService> logger)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _modules = modules;
        _providers = [.. providers];
        _clock = clock;
        _logger = logger;
    }

    public async Task<PersonalDataExport> ExportAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var sections = new List<ExportSection>();

        foreach (var provider in _providers.OrderBy(p => p.Module, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                sections.AddRange(await provider.ExportAsync(userId, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A module that cannot produce its section must not cost the member the other twenty.
                // It is logged and reported as unavailable, which is what it is.
                _logger.LogError(
                    exception,
                    "The {Module} module failed to produce its export section.",
                    provider.Module);

                sections.Add(new ExportSection(provider.Module, "(unavailable)", []));
            }
        }

        return new PersonalDataExport(userId, _clock.UtcNow, sections, Uncovered(_providers.Select(p => p.Module)));
    }

    private string[] Uncovered(IEnumerable<string> covered)
    {
        var have = covered.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _modules.Modules
            .Select(module => module.Name)
            .Where(name => !have.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>
/// Runs an erasure across every registered <see cref="IDataErasureProvider"/>.
/// <para>
/// Scoped for the reason above, and it matters more here than for the export. An erasure provider
/// mutates a tracked aggregate; if this service resolved its providers from a scope of its own, those
/// changes would sit in a <c>DbContext</c> nobody saves and the erasure would silently do nothing —
/// while every <c>ExecuteDelete</c> beside it, which bypasses the change tracker, appeared to work.
/// That is exactly what the first draft did, and the runtime probe is what caught it: sessions and
/// devices gone, the account untouched, and the receipt reporting success.
/// </para>
/// </summary>
public sealed class AccountErasureService : IAccountErasureService
{
    /// <summary>
    /// The module that must go last.
    /// <para>
    /// Every other provider looks the member up while it works. Anonymising the identity first would
    /// leave the rest erasing on behalf of somebody the platform can no longer resolve — and silently,
    /// because a lookup that returns nothing is indistinguishable from a member who held nothing.
    /// </para>
    /// </summary>
    private const string IdentityModule = "Identity";

    private readonly ModuleRegistry _modules;
    private readonly IReadOnlyList<IDataErasureProvider> _providers;
    private readonly IClock _clock;
    private readonly ILogger<AccountErasureService> _logger;

    public AccountErasureService(
        ModuleRegistry modules,
        IEnumerable<IDataErasureProvider> providers,
        IClock clock,
        ILogger<AccountErasureService> logger)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _modules = modules;
        _providers = [.. providers];
        _clock = clock;
        _logger = logger;
    }

    public Task<AccountErasureReceipt> PreviewAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        RunAsync(userId, preview: true, cancellationToken);

    public Task<AccountErasureReceipt> EraseAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        RunAsync(userId, preview: false, cancellationToken);

    private async Task<AccountErasureReceipt> RunAsync(
        Guid userId, bool preview, CancellationToken cancellationToken)
    {
        var ordered = _providers
            .OrderBy(provider => IsIdentity(provider.Module) ? 1 : 0)
            .ThenBy(provider => provider.Module, StringComparer.Ordinal)
            .ToArray();

        var results = new List<ErasureResult>();
        var failures = new List<string>();

        foreach (var provider in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                results.AddRange(preview
                    ? await provider.PreviewAsync(userId, cancellationToken)
                    : await provider.EraseAsync(userId, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Recorded and carried on. Stopping here would leave the member partly erased with
                // nothing saying where it stopped, which is the one outcome worse than a failed module.
                _logger.LogError(
                    exception,
                    "The {Module} module failed to {Action} a member's data.",
                    provider.Module,
                    preview ? "preview erasure of" : "erase");

                failures.Add(provider.Module);
            }
        }

        var covered = ordered.Select(provider => provider.Module).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = _modules.Modules
            .Select(module => module.Name)
            .Where(name => !covered.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (!preview)
        {
            _logger.LogWarning(
                "Erased member {UserId}: {Acted} result(s), {Uncovered} module(s) had no provider, {Failed} failed.",
                userId,
                results.Count,
                uncovered.Length,
                failures.Count);
        }

        return new AccountErasureReceipt(
            userId,
            preview ? null : _clock.UtcNow,
            preview,
            results,
            uncovered,
            failures);
    }

    private static bool IsIdentity(string module) =>
        string.Equals(module, IdentityModule, StringComparison.OrdinalIgnoreCase);
}
