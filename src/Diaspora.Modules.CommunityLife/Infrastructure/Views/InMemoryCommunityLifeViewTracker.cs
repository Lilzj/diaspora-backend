using System.Collections.Concurrent;
using Diaspora.Modules.CommunityLife.Extensibility.Views;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Views;

/// <summary>
/// The shipped view tracker: an in-memory de-duplication window.
/// <para>
/// <b>Per-instance, and that is a deliberate limitation rather than an oversight.</b> Behind two
/// application servers the same reader can be counted unique twice, which inflates the figure by at
/// most the instance count. The alternative is a shared cache on the hot path of every page view,
/// which is a dependency and a latency cost for a number nobody makes a decision from. A deployment
/// that needs the exact figure registers its own implementation — that is what the seam is for.
/// </para>
/// <para>
/// <b>Pruned on a counter rather than a timer.</b> A background timer for a cache that is empty on
/// most deployments is a thread doing nothing; sweeping every thousandth record costs the thousandth
/// caller a scan and nobody else anything.
/// </para>
/// </summary>
public sealed class InMemoryCommunityLifeViewTracker : ICommunityLifeViewTracker
{
    private const int PruneEvery = 1_000;
    private const int Capacity = 50_000;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen =
        new(StringComparer.Ordinal);

    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IClock _clock;

    private long _pruneCounter;

    public InMemoryCommunityLifeViewTracker(
        IOptions<CommunityLifeOptions> options, IClock clock)
    {
        _options = options;
        _clock = clock;
    }

    public bool RecordView(Guid subjectId, string viewerKey)
    {
        if (subjectId == Guid.Empty || string.IsNullOrWhiteSpace(viewerKey))
        {
            return true;
        }

        var now = _clock.UtcNow;
        var window = TimeSpan.FromMinutes(
            Math.Max(1, _options.Value.ViewDeduplicationMinutes));

        var key = $"{subjectId:N}:{viewerKey}";
        var isUnique = true;

        _seen.AddOrUpdate(
            key,
            now,
            (_, last) =>
            {
                if (now - last < window)
                {
                    isUnique = false;

                    return last;
                }

                return now;
            });

        Prune(now, window);

        return isUnique;
    }

    private void Prune(DateTimeOffset now, TimeSpan window)
    {
        if (Interlocked.Increment(ref _pruneCounter) % PruneEvery != 0
            && _seen.Count < Capacity)
        {
            return;
        }

        var cutoff = now - window;

        foreach (var entry in _seen)
        {
            if (entry.Value < cutoff)
            {
                _seen.TryRemove(entry.Key, out _);
            }
        }
    }
}
