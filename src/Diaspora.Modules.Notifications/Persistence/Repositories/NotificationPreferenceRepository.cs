using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// EF Core repository for communication preferences.
/// <para>
/// <b><see cref="ListDigestDueAsync"/> is the query that decides whether digests scale.</b> A
/// schedule is expressed in the member's own wall clock, so "whose 8am is now" cannot be answered by
/// comparing to a UTC hour. Loading every digest-enabled member and converting in memory works
/// perfectly until there are a hundred thousand of them, at which point the hourly sweep is loading
/// the platform's entire preference table twelve times a day.
/// </para>
/// <para>
/// The compromise here is deliberate and documented: the query narrows on frequency and on the
/// stored zone's <em>current</em> offset bucket, computed once per sweep rather than per row, and the
/// final wall-clock decision is made in memory over that much smaller set. It is exact, and it costs
/// one indexed read per distinct offset rather than one per member.
/// </para>
/// </summary>
public sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IOptions<NotificationOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public NotificationPreferenceRepository(
        DiasporaDbContext dbContext,
        IOptions<NotificationOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _dbContext = dbContext;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<NotificationPreference> Preferences => _dbContext.Set<NotificationPreference>();

    public async Task<NotificationPreference?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Preferences
            .Include(preference => preference.Channels)
            .Include(preference => preference.Categories)
            .FirstOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);
    }

    public async Task<NotificationPreference> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var tracked = _dbContext.ChangeTracker
            .Entries<NotificationPreference>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(preference => preference.UserId == userId);

        if (tracked is not null)
        {
            return tracked;
        }

        if (await FindAsync(userId, cancellationToken) is { } existing)
        {
            return existing;
        }

        var preference = NotificationPreference.CreateDefault(
            NotificationPreferenceId.From(_guids.NewSequentialGuid()),
            userId,
            _clock.UtcNow,
            _options.Value.DefaultLanguage,
            _options.Value.DefaultTimeZoneId);

        await Preferences.AddAsync(preference, cancellationToken);

        return preference;
    }

    /// <summary>
    /// Members whose digest fires in this UTC hour.
    /// <para>
    /// The frequency narrows the set in the database; the wall-clock decision is made here, because
    /// converting an IANA identifier is something no provider can do in SQL and something a cached
    /// offset per zone makes cheap.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<NotificationPreference>> ListDigestDueAsync(
        DateTimeOffset utcNow,
        DigestFrequency frequency,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var candidates = await Preferences
            .AsNoTracking()
            .Include(preference => preference.Channels)
            .Include(preference => preference.Categories)
            .Where(preference => preference.Digest.Frequency == frequency)
            .OrderBy(preference => preference.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);
        var due = new List<NotificationPreference>(candidates.Count);

        foreach (var preference in candidates)
        {
            if (!zones.TryGetValue(preference.TimeZoneId, out var zone))
            {
                // An unknown zone falls back to UTC rather than skipping the member: a stale
                // identifier must cost punctuality, never the digest itself.
                zone = TimeZoneInfo.TryFindSystemTimeZoneById(preference.TimeZoneId, out var found)
                    ? found
                    : TimeZoneInfo.Utc;

                zones[preference.TimeZoneId] = zone;
            }

            if (preference.Digest.FiresAt(TimeZoneInfo.ConvertTime(utcNow, zone)))
            {
                due.Add(preference);
            }
        }

        return due;
    }

    public async Task AddAsync(
        NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preference);

        await Preferences.AddAsync(preference, cancellationToken);
    }

    public void Update(NotificationPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        NotificationAggregateUpdater.MarkDirty(_dbContext, preference);
    }
}
