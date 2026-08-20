namespace Diaspora.Modules.Financial.Extensibility.Caching;

/// <summary>
/// A cache this module owns, with a working default behind it.
/// <para>
/// <b>Deliberately not the kernel's <c>ICacheService</c>.</b> That port is declared and
/// unimplemented — the platform's DEBT-013 — and a module depending on one is a module whose seeder
/// fails in silence. B13 and B14 each shipped a module-owned port with a working default for exactly
/// this reason, and an architecture rule now fails the build for anything that depends on a dormant
/// kernel port. This is that pattern, taken deliberately rather than rediscovered.
/// </para>
/// <para>
/// <b>What it is for is the policy and catalogue read path.</b> Resolving a commission rate happens
/// on every sale and reads reference data an operator changes twice a year. The write paths clear
/// the relevant key after the save, never before — clearing in front of a change that then failed to
/// commit re-caches the old row.
/// </para>
/// </summary>
public interface IFinancialCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(
        string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Drops everything under a prefix. What a catalogue change does.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
