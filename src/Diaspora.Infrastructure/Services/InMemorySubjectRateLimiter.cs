using System.Collections.Concurrent;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Infrastructure.Services;

/// <summary>
/// A sliding-window subject limiter held in process memory.
/// <para>
/// Correct on one instance, approximate on several — see <see cref="ISubjectRateLimiter"/>
/// for why that is stated rather than hidden. It exists because "no subject throttling at
/// all", which is what B2 shipped, is materially worse: password spraying one attempt per
/// account never trips a per-account lockout, and nothing else was watching.
/// </para>
/// <para>
/// A sliding window rather than a fixed one. A fixed window lets an attacker spend the
/// whole budget in the last second of one window and the whole budget again in the first
/// second of the next, which is twice the intended rate at exactly the moment it matters.
/// </para>
/// </summary>
public sealed class InMemorySubjectRateLimiter : ISubjectRateLimiter, IDisposable
{
    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private readonly Timer _sweeper;

    public InMemorySubjectRateLimiter(IClock clock)
    {
        _clock = clock;

        // Without this the dictionary is an unbounded, attacker-controlled allocation: one
        // entry per distinct phone number tried. Sweeping every five minutes keeps it
        // proportional to genuine traffic.
        _sweeper = new Timer(
            _ => Sweep(),
            state: null,
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromMinutes(5));
    }

    public ValueTask<RateLimitLease> AcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);

        var now = _clock.UtcNow;
        var entry = _windows.GetOrAdd(key, _ => new Window());

        lock (entry.Gate)
        {
            entry.Prune(now - window);

            if (entry.Attempts.Count >= permitLimit)
            {
                var oldest = entry.Attempts.Peek();
                var retryAfter = oldest + window - now;

                return ValueTask.FromResult(
                    RateLimitLease.Denied(retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(1)));
            }

            entry.Attempts.Enqueue(now);
            entry.LastTouched = now;

            return ValueTask.FromResult(
                RateLimitLease.Allowed(permitLimit - entry.Attempts.Count));
        }
    }

    public ValueTask ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        _windows.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public void Dispose() => _sweeper.Dispose();

    private void Sweep()
    {
        // An hour of inactivity is longer than any configured window, so anything older
        // than that cannot influence a decision.
        var cutoff = _clock.UtcNow - TimeSpan.FromHours(1);

        foreach (var (key, window) in _windows)
        {
            lock (window.Gate)
            {
                if (window.LastTouched < cutoff)
                {
                    _windows.TryRemove(key, out _);
                }
            }
        }
    }

    private sealed class Window
    {
        public Lock Gate { get; } = new();

        public Queue<DateTimeOffset> Attempts { get; } = new();

        public DateTimeOffset LastTouched { get; set; }

        public void Prune(DateTimeOffset before)
        {
            while (Attempts.Count > 0 && Attempts.Peek() <= before)
            {
                Attempts.Dequeue();
            }
        }
    }
}
