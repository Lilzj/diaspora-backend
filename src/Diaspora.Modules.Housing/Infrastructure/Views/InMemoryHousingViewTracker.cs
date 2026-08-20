using System.Collections.Concurrent;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Extensibility.Views;
using Diaspora.Modules.Housing.Options;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Infrastructure.Views;

/// <summary>
/// De-duplicates views in this process.
/// <para>
/// <b>Honest about what it is: a single-node tracker.</b> On a deployment with two web instances a
/// member's views land on two nodes and neither knows about the other, so the unique count is an
/// over-count rather than a truth. That is an acceptable shipped state for a figure rendered as a
/// rough signal on a dashboard, and the seam exists so a cache-backed tracker replaces it without
/// anything above <see cref="IHousingViewTracker"/> changing.
/// </para>
/// <para>
/// <b>An anonymous view always counts as unique</b>, because there is no identity to de-duplicate
/// on and this platform does not track people who have not signed in. That overstates unique views
/// on public traffic and is the honest limit of what can be known without doing something the
/// platform declines to do.
/// </para>
/// <para>
/// A singleton, and safe as one: it holds a concurrent dictionary and nothing scoped. It is also the
/// one seam whose whole value is outliving a request — a per-request tracker would de-duplicate
/// nothing at all.
/// </para>
/// </summary>
public sealed class InMemoryHousingViewTracker : IHousingViewTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly IOptions<HousingOptions> _options;

    private long _pruneCounter;

    public InMemoryHousingViewTracker(IOptions<HousingOptions> options) => _options = options;

    public bool RecordView(HousingListingId listingId, UserId? viewerId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        if (viewerId is null)
        {
            // Nothing to de-duplicate on. See the class summary.
            return true;
        }

        var key = $"{listingId.Value:N}:{viewerId.Value:N}";
        var window = TimeSpan.FromMinutes(
            Math.Max(1, _options.Value.UniqueViewWindowMinutes));

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

    /// <summary>
    /// Drops entries older than the window, occasionally.
    /// <para>
    /// Every few thousand views rather than on every one: pruning is a scan, and doing it on the
    /// hot path would make a popular listing's page slower precisely because it is popular. The
    /// capacity check is the backstop that stops an unpruned dictionary growing without bound on a
    /// quiet deployment.
    /// </para>
    /// </summary>
    private void Prune(DateTimeOffset now, TimeSpan window)
    {
        if (Interlocked.Increment(ref _pruneCounter) % 1_000 != 0
            && _seen.Count < _options.Value.ViewTrackerCapacity)
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
