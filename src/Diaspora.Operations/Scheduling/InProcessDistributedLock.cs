using System.Collections.Concurrent;
using System.Globalization;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Operations.Scheduling;

/// <summary>
/// The shipped <see cref="IDistributedLock"/>: correct within one process, and honest that it is
/// nothing more.
/// <para>
/// <b>This does not close DEBT-015 and does not claim to.</b> Two instances each take their own
/// lease and both run the sweep, exactly as they did before. What it changes is that every sweeper
/// is now written against a lock, so closing the debt is a registration — the database-backed
/// implementation beside this one, or a shared-store adapter — rather than an edit to every sweeper
/// on the platform.
/// </para>
/// <para>
/// Registering a null-object lock that always granted would have been worse than this: it would have
/// looked like a lock in every call site, and the day a real one was registered nobody would know
/// which sweepers had ever been tested against a refusal.
/// </para>
/// </summary>
public sealed class InProcessDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, Lease> _leases = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private readonly string _owner;

    public InProcessDistributedLock(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _owner = string.Create(
            CultureInfo.InvariantCulture,
            $"{Environment.MachineName}/{Environment.ProcessId}");
    }

    public Task<ILeaseHandle?> TryAcquireAsync(
        string resource,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;
        var candidate = new Lease(this, resource, _owner, now + timeToLive);

        while (true)
        {
            if (_leases.TryAdd(resource, candidate))
            {
                return Task.FromResult<ILeaseHandle?>(candidate);
            }

            if (!_leases.TryGetValue(resource, out var existing))
            {
                // Removed between the failed add and the read. Try again.
                continue;
            }

            if (existing.ExpiresAt > now)
            {
                return Task.FromResult<ILeaseHandle?>(null);
            }

            // Expired. Take it over, but only if nobody else did between the read and here — which
            // is what the value-comparing replace guarantees.
            if (_leases.TryUpdate(resource, candidate, existing))
            {
                return Task.FromResult<ILeaseHandle?>(candidate);
            }
        }
    }

    public Task<string?> FindHolderAsync(string resource, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        cancellationToken.ThrowIfCancellationRequested();

        if (_leases.TryGetValue(resource, out var lease) && lease.ExpiresAt > _clock.UtcNow)
        {
            return Task.FromResult<string?>(lease.Owner);
        }

        return Task.FromResult<string?>(null);
    }

    private sealed class Lease : ILeaseHandle
    {
        private readonly InProcessDistributedLock _owner;
        private bool _released;

        public Lease(InProcessDistributedLock owner, string resource, string ownerId, DateTimeOffset expiresAt)
        {
            _owner = owner;
            Resource = resource;
            Owner = ownerId;
            ExpiresAt = expiresAt;
        }

        public string Resource { get; }

        public string Owner { get; }

        public DateTimeOffset ExpiresAt { get; private set; }

        public Task<bool> RenewAsync(TimeSpan extension, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(extension, TimeSpan.Zero);
            cancellationToken.ThrowIfCancellationRequested();

            // Renewal must fail when this lease is no longer the registered one. A holder that
            // renewed after losing its lease would keep working alongside whoever took over, which is
            // the exact condition a lock exists to prevent.
            if (_released || !_owner._leases.TryGetValue(Resource, out var current) || !ReferenceEquals(current, this))
            {
                return Task.FromResult(false);
            }

            ExpiresAt = _owner._clock.UtcNow + extension;

            return Task.FromResult(true);
        }

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _owner._leases.TryRemove(new KeyValuePair<string, Lease>(Resource, this));
            }

            return ValueTask.CompletedTask;
        }
    }
}
