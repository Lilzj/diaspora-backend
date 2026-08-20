using System.Buffers.Binary;
using System.Collections.Concurrent;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Operations.Caching;

/// <summary>
/// Holds the version counters that make namespace and tag invalidation cheap.
/// <para>
/// <b>A version is a timestamp, not a counter.</b> Incrementing a counter needs read-modify-write,
/// which two replicas invalidating at the same instant will lose one half of — and the half that is
/// lost is an invalidation, so the cache keeps serving something it was told to stop serving.
/// Writing the current tick instead is monotonic by construction, needs no read, and two concurrent
/// invalidations produce two nearly-equal numbers, both of which are greater than every version any
/// live entry was written under. That is all invalidation needs.
/// </para>
/// <para>
/// Versions live in the shared tier so every replica sees them, and are cached in this process for
/// <see cref="CacheOptions.VersionRefreshInterval"/>. <b>That interval is the propagation delay of
/// every tag and namespace invalidation on the platform</b>, and it is stated as plainly here as it
/// is in the options, because a caller who does not know it will eventually write a test that reads
/// back immediately and expects a miss.
/// </para>
/// </summary>
public sealed class CacheVersionStore
{
    private const string VersionKeyspace = "__ver";

    /// <summary>
    /// Versions outlive the entries they invalidate by a wide margin. A version that expired sooner
    /// than the longest-lived entry carrying it would reset to zero and un-invalidate that entry —
    /// a cache serving content it was explicitly told to drop, hours later, for no visible reason.
    /// </summary>
    private static readonly TimeSpan VersionLifetime = TimeSpan.FromDays(7);

    private readonly ConcurrentDictionary<string, CachedVersion> _local = new(StringComparer.Ordinal);
    private readonly ICacheStore _store;
    private readonly IClock _clock;
    private readonly string _prefix;
    private readonly TimeSpan _refreshInterval;

    public CacheVersionStore(ICacheStore sharedStore, IClock clock, IOptions<CacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(sharedStore);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _store = sharedStore;
        _clock = clock;
        _prefix = options.Value.KeyPrefix;
        _refreshInterval = options.Value.VersionRefreshInterval;
    }

    /// <summary>The current version of a scope. Zero when it has never been invalidated.</summary>
    public async Task<long> GetAsync(string scope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = _clock.UtcNow;

        if (_local.TryGetValue(scope, out var cached) && now - cached.ReadAt < _refreshInterval)
        {
            return cached.Version;
        }

        long version = 0;

        try
        {
            var read = await _store.GetAsync(StorageKey(scope), cancellationToken).ConfigureAwait(false);

            if (read.Found && read.Value.Length == sizeof(long))
            {
                version = BinaryPrimitives.ReadInt64LittleEndian(read.Value.Span);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // An unreadable version store must not fail the read that consulted it. Falling back to
            // the last known version means a recent invalidation may be missed for as long as the
            // store is down — strictly better than failing every cached read on the platform.
            return cached.Version;
        }

        _local[scope] = new CachedVersion(version, now);

        return version;
    }

    /// <summary>Several scopes at once, for an entry carrying several tags.</summary>
    public async Task<IReadOnlyDictionary<string, long>> GetManyAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        if (scopes.Count == 0)
        {
            return EmptyVersions;
        }

        var versions = new Dictionary<string, long>(scopes.Count, StringComparer.Ordinal);

        foreach (var scope in scopes)
        {
            versions[scope] = await GetAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        return versions;
    }

    /// <summary>
    /// Invalidates a scope by writing the current tick.
    /// <para>
    /// The local copy is updated first and unconditionally, so the replica that performed the
    /// invalidation observes it immediately even if the shared write fails. A caller that just
    /// changed something and then reads it back on the same replica sees the change; the other
    /// replicas see it within the refresh interval.
    /// </para>
    /// </summary>
    public async Task<long> BumpAsync(string scope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = _clock.UtcNow;
        var version = now.UtcTicks;

        _local[scope] = new CachedVersion(version, now);

        var payload = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(payload, version);

        try
        {
            await _store.SetAsync(
                StorageKey(scope), payload, VersionLifetime, slidingExpiration: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Recorded by the caller, which owns the error counter. Swallowed here because a failed
            // invalidation on the shared tier still succeeded on this replica, and throwing would
            // fail the command that legitimately changed the data.
        }

        return version;
    }

    /// <summary>Drops this process's cached versions. Used after a full flush.</summary>
    public void Forget() => _local.Clear();

    private static readonly IReadOnlyDictionary<string, long> EmptyVersions =
        new Dictionary<string, long>(StringComparer.Ordinal);

    private string StorageKey(string scope) =>
        $"{_prefix}{CacheKey.Separator}{VersionKeyspace}{CacheKey.Separator}{scope}";

    private readonly record struct CachedVersion(long Version, DateTimeOffset ReadAt);
}
