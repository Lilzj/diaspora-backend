using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diaspora.Platform.Modules.Seeding;

/// <summary>
/// Runs every discovered <see cref="IReferenceDataSeeder"/> once, in a deterministic order.
/// <para>
/// <b>This replaces a hand-written list, and the list is the reason it exists.</b> The integration
/// harness used to name twelve seeders one at a time, each behind a <c>using</c> on a module's
/// internal hosting namespace. A thirteenth module that forgot to add itself did not fail one test:
/// its catalogue stayed empty, the first request that needed a category threw, and every test in
/// the suite died at host start naming neither the module nor the seeder. B8 recorded that, B8.1
/// recorded it again after paying it a second time, and PSR-1 is where the list stops being a list.
/// </para>
/// <para>
/// <b>Order is derived, never restated.</b> Seeders run in their owning module's registered order,
/// then by the seeder's own <see cref="IReferenceDataSeeder.Order"/>, then by name. A module that
/// repeated its order number on its seeder would own two copies of one fact, free to drift — the
/// exact failure the architecture suite records for hard-coded module lists. A seeder whose module
/// is not in the registry sorts last rather than disappearing, because a catalogue seeded late is
/// recoverable and one never seeded is not.
/// </para>
/// <para>
/// <b>A failing seeder does not stop the others, and never stops the host.</b> Each is invoked
/// inside its own try/catch: one catalogue that cannot be written must not cost the platform the
/// eleven that can. This is the same contract every individual seeder already holds, restated in
/// the one place that now calls all of them.
/// </para>
/// </summary>
public sealed class ReferenceDataSeedRunner
{
    private readonly IReadOnlyList<IReferenceDataSeeder> _seeders;
    private readonly ModuleRegistry? _modules;
    private readonly ILogger<ReferenceDataSeedRunner> _logger;

    public ReferenceDataSeedRunner(
        IEnumerable<IReferenceDataSeeder> seeders,
        ModuleRegistry? modules = null,
        ILogger<ReferenceDataSeedRunner>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(seeders);

        _modules = modules;
        _logger = logger ?? NullLogger<ReferenceDataSeedRunner>.Instance;
        _seeders = Order(seeders, modules);
    }

    /// <summary>Every discovered seeder, in the order <see cref="SeedAllAsync"/> will run them.</summary>
    public IReadOnlyList<IReferenceDataSeeder> Seeders => _seeders;

    /// <summary>
    /// Seeder names claimed by more than one implementation.
    /// <para>
    /// Reported rather than thrown on. Two seeders sharing a name is a defect in a module's own
    /// declaration and the startup validator says so at boot; refusing to seed anything because of
    /// it would turn one module's naming mistake into a platform with no catalogues at all.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> DuplicateNames =>
        [.. _seeders
            .GroupBy(seeder => seeder.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)];

    /// <summary>
    /// Runs every seeder in order. Never throws: a seeder that fails is logged and the next one
    /// runs.
    /// </summary>
    /// <returns>How many seeders completed without throwing.</returns>
    public async Task<int> SeedAllAsync(CancellationToken cancellationToken = default)
    {
        var seeded = 0;

        foreach (var seeder in _seeders)
        {
            try
            {
                await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
                seeded++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Reference-data seeder {Seeder} (module {Module}) failed. Its catalogue may be " +
                    "incomplete; the remaining seeders still ran.",
                    seeder.Name,
                    seeder.Module);
            }
        }

        return seeded;
    }

    /// <summary>
    /// Module order first, then the seeder's own tie-break, then name — so two runs of the same
    /// host seed in the same sequence, which is what makes a failure reproducible.
    /// </summary>
    private static IReadOnlyList<IReferenceDataSeeder> Order(
        IEnumerable<IReferenceDataSeeder> seeders, ModuleRegistry? modules)
    {
        var moduleOrder = modules?.Modules
            .ToDictionary(
                registration => registration.Name,
                registration => registration.Module.Order,
                StringComparer.OrdinalIgnoreCase);

        return [.. seeders
            .OrderBy(seeder => ModuleOrder(seeder, moduleOrder))
            .ThenBy(seeder => seeder.Order)
            .ThenBy(seeder => seeder.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// A seeder whose module the registry does not know sorts last. It is still seeded: an unknown
    /// module name is a metadata mistake the module validator reports, and dropping the catalogue
    /// would turn that into a platform that cannot serve the module at all.
    /// </summary>
    private static int ModuleOrder(
        IReferenceDataSeeder seeder, Dictionary<string, int>? moduleOrder) =>
        moduleOrder is not null && moduleOrder.TryGetValue(seeder.Module, out var order)
            ? order
            : int.MaxValue;
}
