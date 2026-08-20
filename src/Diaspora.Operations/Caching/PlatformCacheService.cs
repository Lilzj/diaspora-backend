using System.Collections.Concurrent;
using System.Diagnostics;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Operations.Caching;

/// <summary>
/// The platform cache: policy-aware, multi-level, tagged, and provider-independent.
/// <para>
/// One implementation of <see cref="ICacheService"/> for every deployment. Which stores are behind
/// it is a registration detail — a single in-process store today, a shared store beside it the day
/// one is configured — and no caller changes when that changes.
/// </para>
/// <para>
/// <b>The read path, in order.</b> Local tier, then shared tier (promoting a hit back into the
/// local tier), then the factory. Every hit is checked against its tag versions before it is
/// believed, and an entry past its absolute expiry may still be served once under
/// <c>StaleWhileRevalidate</c> while one caller refreshes it.
/// </para>
/// <para>
/// <b>Every store fault is swallowed and counted.</b> Unless the policy says
/// <c>FailOnCacheError</c>, a cache that throws behaves as a cache that missed. The counter is what
/// makes that honest: an operator watching only the hit ratio would see a healthy cache while the
/// shared tier was completely unreachable, because every failed shared read falls through to a
/// successful factory call.
/// </para>
/// </summary>
public sealed class PlatformCacheService : ICacheService, ICacheInvalidator, ICacheStatisticsProvider, IDisposable
{
    private static readonly CachePolicy Fallback = new(OperationalPolicyNames.Cache.Default)
    {
        Description = "Compiled-in fallback, used when the catalogue has no policy by that name."
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Counters> _counters = new(StringComparer.Ordinal);

    private readonly ICacheStore _local;
    private readonly ICacheStore? _distributed;
    private readonly CacheVersionStore _versions;
    private readonly IOperationalPolicyCatalog _policies;
    private readonly IClock _clock;
    private readonly ILogger<PlatformCacheService> _logger;
    private readonly CacheOptions _options;

    public PlatformCacheService(
        IEnumerable<ICacheStore> stores,
        CacheVersionStore versions,
        IOperationalPolicyCatalog policies,
        IClock clock,
        IOptions<CacheOptions> options,
        ILogger<PlatformCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var registered = stores as IReadOnlyList<ICacheStore> ?? [.. stores];

        _local = registered.LastOrDefault(store => store.Tier == CacheStoreTier.Local)
            ?? throw new InvalidOperationException(
                "No local cache store is registered. The platform cache always has a local tier — " +
                "a deployment turns caching off with Cache:Enabled, not by removing the store.");

        // Last registration wins, so a deployment adding a shared store after the defaults gets it
        // without removing anything. Null is a supported and reported state, not a failure: the
        // dashboard says so, because a multi-level policy served entirely from process memory is
        // inconsistent across replicas and an operator must be able to see that rather than infer it.
        _distributed = registered.LastOrDefault(store => store.Tier == CacheStoreTier.Distributed);

        _versions = versions;
        _policies = policies;
        _clock = clock;
        _logger = logger;
        _options = options.Value;

        if (_distributed is null)
        {
            _logger.LogInformation(
                "No shared cache store is registered. Distributed and multi-level policies are being " +
                "served from process memory, so entries differ between replicas and an invalidation " +
                "reaches only the replica that performed it.");
        }
    }

    // ─── Reads ───────────────────────────────────────────────────────────────────────────

    public async Task<T> GetOrCreateAsync<T>(
        CacheKey key,
        string policyName,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        EnsureUsable(key);

        if (!_options.Enabled)
        {
            return await factory(cancellationToken).ConfigureAwait(false);
        }

        var policy = Resolve(policyName);
        var stopwatch = ValueStopwatch.Start();

        var read = await TryReadAsync<T>(key, policy, cancellationToken).ConfigureAwait(false);

        if (read.Source is CacheSource.Local or CacheSource.Distributed)
        {
            Record(key.Namespace, read.Source, stopwatch);
            return read.Value!;
        }

        // A stale hit is returned to this caller immediately and refreshed in the background. The
        // refresh is deliberately not awaited: the entire value of stale-while-revalidate is that
        // the caller does not pay for the recomputation.
        if (read.Source == CacheSource.Stale)
        {
            Record(key.Namespace, CacheSource.Stale, stopwatch);
            RefreshInBackground(key, policy, factory);
            return read.Value!;
        }

        // ─── Miss. One caller per key computes; the rest wait for it. ───
        var storageKey = await StorageKeyAsync(key, cancellationToken).ConfigureAwait(false);
        var gate = _gates.GetOrAdd(storageKey, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Re-read behind the gate. The caller that was waiting is very likely waiting for a
            // value that has just been written, and skipping this check would make the stampede
            // protection pointless — every waiter would run the factory the instant it was let through.
            var second = await TryReadAsync<T>(key, policy, cancellationToken).ConfigureAwait(false);

            if (second.Source is CacheSource.Local or CacheSource.Distributed)
            {
                Record(key.Namespace, second.Source, stopwatch);
                return second.Value!;
            }

            var value = await factory(cancellationToken).ConfigureAwait(false);

            await WriteAsync(key, value, policy, cancellationToken).ConfigureAwait(false);

            Record(key.Namespace, CacheSource.Miss, stopwatch);

            return value;
        }
        finally
        {
            gate.Release();

            // Atomic on key *and* value, so a gate another caller has already replaced is never
            // removed. The residual race — a caller holding a reference to a gate this line has just
            // removed — costs at most one extra factory run, which is what a cache miss costs anyway.
            if (gate.CurrentCount == 1)
            {
                _gates.TryRemove(new KeyValuePair<string, SemaphoreSlim>(storageKey, gate));
            }
        }
    }

    public async Task<T?> GetAsync<T>(
        CacheKey key, string policyName, CancellationToken cancellationToken = default)
    {
        EnsureUsable(key);

        if (!_options.Enabled)
        {
            return default;
        }

        var stopwatch = ValueStopwatch.Start();
        var read = await TryReadAsync<T>(key, Resolve(policyName), cancellationToken).ConfigureAwait(false);

        Record(key.Namespace, read.Source, stopwatch);

        return read.Source == CacheSource.Miss ? default : read.Value;
    }

    public async Task<bool> ExistsAsync(
        CacheKey key, string policyName, CancellationToken cancellationToken = default)
    {
        EnsureUsable(key);

        if (!_options.Enabled)
        {
            return false;
        }

        var read = await TryReadAsync<object>(key, Resolve(policyName), cancellationToken)
            .ConfigureAwait(false);

        return read.Source != CacheSource.Miss;
    }

    // ─── Writes ──────────────────────────────────────────────────────────────────────────

    public async Task SetAsync<T>(
        CacheKey key, T value, string policyName, CancellationToken cancellationToken = default)
    {
        EnsureUsable(key);

        if (!_options.Enabled)
        {
            return;
        }

        await WriteAsync(key, value, Resolve(policyName), cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        EnsureUsable(key);

        var storageKey = await StorageKeyAsync(key, cancellationToken).ConfigureAwait(false);

        await SafeAsync(key.Namespace, () => _local.RemoveAsync(storageKey, cancellationToken))
            .ConfigureAwait(false);

        if (_distributed is not null)
        {
            await SafeAsync(key.Namespace, () => _distributed.RemoveAsync(storageKey, cancellationToken))
                .ConfigureAwait(false);
        }
    }

    public Task RemoveNamespaceAsync(string cacheNamespace, CancellationToken cancellationToken = default) =>
        InvalidateNamespaceAsync(cacheNamespace, CacheInvalidationReason.Version, cancellationToken);

    // ─── Invalidation ────────────────────────────────────────────────────────────────────

    public async Task InvalidateTagAsync(
        string tag,
        CacheInvalidationReason reason = CacheInvalidationReason.Tag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        await _versions.BumpAsync(TagScope(tag), cancellationToken).ConfigureAwait(false);

        OperationsTelemetry.CacheInvalidations.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Reason, reason.ToString()),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, tag));

        _logger.LogInformation(
            "Cache tag {Tag} invalidated ({Reason}).", tag, reason);
    }

    public async Task InvalidateNamespaceAsync(
        string cacheNamespace,
        CacheInvalidationReason reason = CacheInvalidationReason.Version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheNamespace);

        await _versions.BumpAsync(NamespaceScope(cacheNamespace), cancellationToken).ConfigureAwait(false);

        Counter(cacheNamespace).Invalidations++;

        OperationsTelemetry.CacheInvalidations.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Reason, reason.ToString()),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.CacheNamespace, cacheNamespace));

        _logger.LogInformation(
            "Cache namespace {CacheNamespace} invalidated ({Reason}).", cacheNamespace, reason);
    }

    public Task InvalidateKeyAsync(
        CacheKey key,
        CacheInvalidationReason reason = CacheInvalidationReason.Key,
        CancellationToken cancellationToken = default)
    {
        Counter(key.Namespace).Invalidations++;

        OperationsTelemetry.CacheInvalidations.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Reason, reason.ToString()),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.CacheNamespace, key.Namespace));

        return RemoveAsync(key, cancellationToken);
    }

    /// <summary>
    /// Invalidates everything, by bumping the root scope every storage key derives from.
    /// <para>
    /// A genuinely dangerous operation and it is worth saying so where it is implemented: a platform
    /// that has just discarded its whole cache sends every read to the database at once. The button
    /// exists because there are incidents where it is the right one; it is audited, and the
    /// dashboard shows when it was last pressed.
    /// </para>
    /// </summary>
    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        await _versions.BumpAsync(RootScope, cancellationToken).ConfigureAwait(false);

        _versions.Forget();

        OperationsTelemetry.CacheInvalidations.Add(1,
            new KeyValuePair<string, object?>(
                PlatformTelemetryNames.Attributes.Reason, CacheInvalidationReason.Manual.ToString()),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.CacheNamespace, "*"));

        _logger.LogWarning(
            "The entire platform cache was invalidated. Every read will reach its source until the " +
            "cache refills.");
    }

    // ─── Statistics ──────────────────────────────────────────────────────────────────────

    public CacheStatistics Snapshot() => new(
        _local.Provider,
        _distributed?.Provider,
        [.. _counters
            .Select(entry => new CacheNamespaceStatistics(
                entry.Key,
                entry.Value.Hits,
                entry.Value.Misses,
                entry.Value.StaleServes,
                entry.Value.Writes,
                entry.Value.Invalidations,
                entry.Value.Errors))
            .OrderBy(entry => entry.Namespace, StringComparer.Ordinal)]);

    public void Dispose()
    {
        foreach (var gate in _gates.Values)
        {
            gate.Dispose();
        }

        _gates.Clear();
    }

    // ─── Internals ───────────────────────────────────────────────────────────────────────

    private const string RootScope = "__all";

    private CachePolicy Resolve(string policyName) =>
        string.IsNullOrWhiteSpace(policyName)
            ? Fallback
            : _policies.Find<CachePolicy>(policyName)
                ?? _policies.Find<CachePolicy>(OperationalPolicyNames.Cache.Default)
                ?? Fallback;

    private static void EnsureUsable(CacheKey key)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException(
                "A cache key must name a namespace. Build one with CacheKey.For.", nameof(key));
        }
    }

    private async Task<(CacheSource Source, T? Value)> TryReadAsync<T>(
        CacheKey key, CachePolicy policy, CancellationToken cancellationToken)
    {
        var storageKey = await StorageKeyAsync(key, cancellationToken).ConfigureAwait(false);

        if (policy.Tier is Platform.Operations.Policies.CacheTier.Local or
            Platform.Operations.Policies.CacheTier.MultiLevel)
        {
            var local = await ReadFromAsync<T>(_local, storageKey, key, policy, cancellationToken)
                .ConfigureAwait(false);

            if (local.Source != CacheSource.Miss)
            {
                return local;
            }
        }

        if (_distributed is null ||
            policy.Tier == Platform.Operations.Policies.CacheTier.Local)
        {
            return (CacheSource.Miss, default);
        }

        var shared = await ReadFromAsync<T>(_distributed, storageKey, key, policy, cancellationToken)
            .ConfigureAwait(false);

        if (shared.Source == CacheSource.Miss)
        {
            return (CacheSource.Miss, default);
        }

        // Promote into the local tier so the next read on this replica does not cross the network.
        // Only for a multi-level policy: a policy that asked for the shared tier alone did so because
        // a per-replica copy would be wrong.
        if (policy.Tier == Platform.Operations.Policies.CacheTier.MultiLevel && shared.Value is not null)
        {
            await WriteToAsync(_local, storageKey, key, shared.Value, policy, localOnly: true, cancellationToken)
                .ConfigureAwait(false);
        }

        return (shared.Source == CacheSource.Stale ? CacheSource.Stale : CacheSource.Distributed, shared.Value);
    }

    private async Task<(CacheSource Source, T? Value)> ReadFromAsync<T>(
        ICacheStore store,
        string storageKey,
        CacheKey key,
        CachePolicy policy,
        CancellationToken cancellationToken)
    {
        CacheStoreRead read;

        try
        {
            read = await store.GetAsync(storageKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HandleStoreFault<T>(key, policy, store, exception, "read");
        }

        if (!read.Found)
        {
            return (CacheSource.Miss, default);
        }

        var envelope = CacheSerializer.ReadEnvelope(read.Value);

        if (envelope is null)
        {
            return (CacheSource.Miss, default);
        }

        if (!await TagsAreCurrentAsync(envelope, cancellationToken).ConfigureAwait(false))
        {
            return (CacheSource.Miss, default);
        }

        var age = _clock.UtcNow - new DateTimeOffset(envelope.WrittenAtTicks, TimeSpan.Zero);

        // The store's own expiry has not necessarily fired: an entry written with a
        // stale-while-revalidate window is stored for expiry *plus* the window, so this is the check
        // that decides which side of the line the entry is on.
        if (policy.StaleWhileRevalidate is not null && age > policy.AbsoluteExpiration)
        {
            return (CacheSource.Stale, CacheSerializer.ReadValue<T>(envelope));
        }

        return (store.Tier == CacheStoreTier.Local ? CacheSource.Local : CacheSource.Distributed,
            CacheSerializer.ReadValue<T>(envelope));
    }

    private async Task<bool> TagsAreCurrentAsync(CacheEnvelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.TagVersions is not { Count: > 0 })
        {
            return true;
        }

        foreach (var (tag, writtenVersion) in envelope.TagVersions)
        {
            var current = await _versions.GetAsync(TagScope(tag), cancellationToken).ConfigureAwait(false);

            if (current > writtenVersion)
            {
                return false;
            }
        }

        return true;
    }

    private async Task WriteAsync<T>(
        CacheKey key, T value, CachePolicy policy, CancellationToken cancellationToken)
    {
        // Null is never cached. Caching a null means a lookup that legitimately found nothing is
        // indistinguishable from a lookup that has not run — and the entry that says "nothing" is the
        // one that survives the write that would have made it something.
        if (value is null)
        {
            return;
        }

        var storageKey = await StorageKeyAsync(key, cancellationToken).ConfigureAwait(false);

        var writesLocal = policy.Tier is Platform.Operations.Policies.CacheTier.Local or
            Platform.Operations.Policies.CacheTier.MultiLevel;

        var writesShared = _distributed is not null &&
            policy.Tier is Platform.Operations.Policies.CacheTier.Distributed or
                Platform.Operations.Policies.CacheTier.MultiLevel;

        if (writesLocal)
        {
            await WriteToAsync(_local, storageKey, key, value, policy, localOnly: true, cancellationToken)
                .ConfigureAwait(false);
        }

        if (writesShared)
        {
            await WriteToAsync(_distributed!, storageKey, key, value, policy, localOnly: false, cancellationToken)
                .ConfigureAwait(false);
        }

        Counter(key.Namespace).Writes++;
    }

    private async Task WriteToAsync<T>(
        ICacheStore store,
        string storageKey,
        CacheKey key,
        T value,
        CachePolicy policy,
        bool localOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            var tagVersions = policy.Tags.Count == 0
                ? null
                : await _versions.GetManyAsync(
                    [.. policy.Tags], cancellationToken).ConfigureAwait(false);

            // Tag scopes are versioned, but the envelope records the tag by its plain name so a
            // reader can compare without knowing the scope encoding.
            var payload = CacheSerializer.Serialize(value, tagVersions, _clock.UtcNow.UtcTicks);

            var absolute = policy.AbsoluteExpiration + (policy.StaleWhileRevalidate ?? TimeSpan.Zero);
            var sliding = policy.SlidingExpiration;

            if (localOnly && policy.LocalExpiration is { } local)
            {
                absolute = local + (policy.StaleWhileRevalidate ?? TimeSpan.Zero);
                sliding = null;
            }

            await store.SetAsync(storageKey, payload, absolute, sliding, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleStoreFault<T>(key, policy, store, exception, "write");
        }
    }

    private (CacheSource Source, T? Value) HandleStoreFault<T>(
        CacheKey key, CachePolicy policy, ICacheStore store, Exception exception, string operation)
    {
        Counter(key.Namespace).Errors++;

        OperationsTelemetry.CacheErrors.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.CacheNamespace, key.Namespace),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Provider, store.Provider),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, operation));

        if (policy.FailOnCacheError)
        {
            throw new InvalidOperationException(
                $"The '{store.Provider}' cache store failed on {operation} and policy '{policy.Name}' " +
                "declares FailOnCacheError.", exception);
        }

        _logger.LogWarning(
            exception,
            "Cache {Operation} failed on the {Provider} store for namespace {CacheNamespace}. " +
            "Treated as a miss.",
            operation, store.Provider, key.Namespace);

        return (CacheSource.Miss, default);
    }

    private void RefreshInBackground<T>(CacheKey key, CachePolicy policy, Func<CancellationToken, Task<T>> factory)
    {
        // Fire-and-forget with a fresh token. The originating request's token is about to be
        // cancelled when its response is written, which would cancel the refresh the caller just
        // paid a stale answer to get started.
        _ = Task.Run(async () =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            try
            {
                var value = await factory(timeout.Token).ConfigureAwait(false);
                await WriteAsync(key, value, policy, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A background refresh that throws must not take the process down, and there is no
                // caller to return the failure to. The stale entry stays and the next read tries again.
                _logger.LogWarning(
                    exception,
                    "Background refresh of {CacheKey} failed. The stale entry remains until it expires.",
                    key);
            }
        });
    }

    private async Task<string> StorageKeyAsync(CacheKey key, CancellationToken cancellationToken)
    {
        var root = await _versions.GetAsync(RootScope, cancellationToken).ConfigureAwait(false);
        var ns = await _versions.GetAsync(NamespaceScope(key.Namespace), cancellationToken).ConfigureAwait(false);

        // The two versions are combined rather than nested so that either one changing produces a
        // different key. Addition is enough: both are tick counts, and a collision would need two
        // invalidations whose tick sums matched exactly, on the same namespace, within its lifetime.
        return key.ToStorageKey(_options.KeyPrefix, root + ns);
    }

    private static string NamespaceScope(string cacheNamespace) => "ns:" + cacheNamespace;

    private static string TagScope(string tag) => "tag:" + tag;

    private Counters Counter(string cacheNamespace) =>
        _counters.GetOrAdd(cacheNamespace, static _ => new Counters());

    private void Record(string cacheNamespace, CacheSource source, ValueStopwatch stopwatch)
    {
        var counters = Counter(cacheNamespace);

        switch (source)
        {
            case CacheSource.Local:
            case CacheSource.Distributed:
                counters.Hits++;
                break;
            case CacheSource.Stale:
                counters.StaleServes++;
                break;
            default:
                counters.Misses++;
                break;
        }

        var tags = new TagList
        {
            { PlatformTelemetryNames.Attributes.CacheNamespace, cacheNamespace },
            { PlatformTelemetryNames.Attributes.CacheResult, ResultLabel(source) },
            { PlatformTelemetryNames.Attributes.Provider, _distributed?.Provider ?? _local.Provider }
        };

        OperationsTelemetry.CacheOperations.Add(1, tags);
        OperationsTelemetry.CacheDuration.Record(stopwatch.ElapsedMilliseconds, tags);
    }

    /// <summary>
    /// The metric label for a read result.
    /// <para>
    /// A switch over interned literals rather than <c>ToString().ToLowerInvariant()</c>, which
    /// allocates twice on a path that runs on every cached read on the platform.
    /// </para>
    /// </summary>
    private static string ResultLabel(CacheSource source) => source switch
    {
        CacheSource.Local => "hit_local",
        CacheSource.Distributed => "hit_distributed",
        CacheSource.Stale => "stale",
        _ => "miss"
    };

    private async Task SafeAsync(string cacheNamespace, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Counter(cacheNamespace).Errors++;
            _logger.LogWarning(exception, "A cache operation failed for namespace {CacheNamespace}.", cacheNamespace);
        }
    }

    /// <summary>
    /// Mutable counters, held per namespace.
    /// <para>
    /// Plain fields rather than <c>Interlocked</c>. These are diagnostic counts read by a dashboard
    /// that refreshes every few seconds; paying an interlocked increment on every cache read to make
    /// a displayed number exact would cost more than the numbers are worth, and a lost increment
    /// under contention changes a ratio in the fourth decimal place. The exported metric, which is
    /// what alerts fire on, goes through the OpenTelemetry counter and is correct.
    /// </para>
    /// </summary>
    private sealed class Counters
    {
        public long Hits;
        public long Misses;
        public long StaleServes;
        public long Writes;
        public long Invalidations;
        public long Errors;
    }

    /// <summary>Allocation-free elapsed time. A <c>Stopwatch</c> per cache read is a class per read.</summary>
    private readonly struct ValueStopwatch
    {
        private readonly long _start;

        private ValueStopwatch(long start) => _start = start;

        public static ValueStopwatch Start() => new(Stopwatch.GetTimestamp());

        public double ElapsedMilliseconds =>
            Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
    }
}
