using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Allocates a deep-link handle that nothing else already holds.
/// <para>
/// <b>Collisions are the normal case here, not the exception.</b> Two associations in different
/// cities really are both called "Community Centre", and a church runs "Sunday Service" fifty-two
/// times a year. The allocator appends a discriminator rather than refusing the write, because
/// refusing would make the second one's editor rename something they named correctly.
/// </para>
/// <para>
/// <b>Uniqueness is checked against deleted rows too.</b> A slug is a permanent link — the one
/// shared to a funeral is pinned in messages years old — and reusing one would silently redirect
/// every message that carries it.
/// </para>
/// </summary>
public sealed class CommunityLifeSlugAllocator
{
    private const int MaxAttempts = 12;

    private readonly IOrganizationRepository _organizations;
    private readonly ICommunityEventRepository _events;
    private readonly IEventSeriesRepository _series;

    public CommunityLifeSlugAllocator(
        IOrganizationRepository organizations,
        ICommunityEventRepository events,
        IEventSeriesRepository series)
    {
        _organizations = organizations;
        _events = events;
        _series = series;
    }

    /// <summary>A free handle for an organization.</summary>
    public async Task<CommunityLifeSlug> ForOrganizationAsync(
        string? name, CancellationToken cancellationToken = default) =>
        await AllocateAsync(
            name,
            null,
            slug => _organizations.SlugExistsAsync(slug, cancellationToken));

    /// <summary>
    /// A free handle for an event.
    /// <para>
    /// The date is the first discriminator rather than a number, because "sunday-service-2026-08-09"
    /// is a link somebody can read and "sunday-service-7" is not.
    /// </para>
    /// </summary>
    public async Task<CommunityLifeSlug> ForEventAsync(
        string? title,
        DateTimeOffset startsAt,
        CancellationToken cancellationToken = default) =>
        await ForEventAsync(title, startsAt, reserved: null, cancellationToken);

    /// <summary>
    /// A free handle for an event, treating a caller-supplied set as already taken.
    /// <para>
    /// <b>B12.1, and it exists because uniqueness in the database is not uniqueness in a
    /// transaction.</b> Expanding a series writes twenty-six events in one unit of work, and
    /// <see cref="ICommunityEventRepository.SlugExistsAsync"/> can only see what has been committed
    /// — so every one of the twenty-six was offered the same free handle and the insert failed on
    /// the unique index. The reserved set is what the run has already issued.
    /// </para>
    /// </summary>
    public async Task<CommunityLifeSlug> ForEventAsync(
        string? title,
        DateTimeOffset startsAt,
        ISet<string>? reserved,
        CancellationToken cancellationToken = default) =>
        await AllocateAsync(
            title,
            startsAt.UtcDateTime.ToString(
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            async slug => reserved?.Contains(slug) == true
                || await _events.SlugExistsAsync(slug, cancellationToken));

    /// <summary>
    /// A free handle for a repeating event.
    /// <para>
    /// <b>Checked against the series table only, and that is deliberate.</b> A series and its
    /// occurrences are different resources at different routes — <c>/events/series/{slug}</c> and
    /// <c>/events/{slug}</c> — so "sunday-service" as a series and "sunday-service-2026-08-09" as one
    /// of its dates coexist without ambiguity. Sharing one namespace would mean the series took the
    /// clean handle and every occurrence got a number.
    /// </para>
    /// <para>
    /// No date discriminator, unlike an event's: a series has no single date to name it by.
    /// </para>
    /// </summary>
    public async Task<CommunityLifeSlug> ForSeriesAsync(
        string? title, CancellationToken cancellationToken = default) =>
        await AllocateAsync(
            title,
            null,
            slug => _series.SlugExistsAsync(slug, cancellationToken));

    private static async Task<CommunityLifeSlug> AllocateAsync(
        string? text, string? firstDiscriminator, Func<string, Task<bool>> exists)
    {
        var candidate = CommunityLifeSlug.FromText(text);

        if (!await exists(candidate.Value))
        {
            return candidate;
        }

        if (firstDiscriminator is not null)
        {
            candidate = CommunityLifeSlug.FromText(text, firstDiscriminator);

            if (!await exists(candidate.Value))
            {
                return candidate;
            }
        }

        for (var attempt = 2; attempt <= MaxAttempts; attempt++)
        {
            var suffix = firstDiscriminator is null
                ? attempt.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{firstDiscriminator}-{attempt}";

            candidate = CommunityLifeSlug.FromText(text, suffix);

            if (!await exists(candidate.Value))
            {
                return candidate;
            }
        }

        // Twelve collisions on one name means something is generating them. A short random suffix
        // terminates rather than looping, and produces a link that still works.
        return CommunityLifeSlug.FromText(
            text, Guid.NewGuid().ToString("N")[..8]);
    }
}
