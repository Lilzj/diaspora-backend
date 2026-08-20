using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// One piece of content, flattened for indexing.
/// <para>
/// Deliberately flat, deliberately denormalised, and deliberately carrying the audience. An
/// index that stores no visibility can only be queried by a process that re-checks every hit
/// against the database, which is the design that makes search slower than the feed it was meant
/// to accelerate.
/// </para>
/// <para>
/// <see cref="IsDeleted"/> is present because a search projection that only emits live content
/// can never tell an index to <em>remove</em> something — the classic way deleted posts stay
/// searchable for months.
/// </para>
/// </summary>
public sealed record SearchProjection
{
    public required ContentReference Content { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required Guid AuthorId { get; init; }

    public string? Category { get; init; }

    public IReadOnlyCollection<string> Hashtags { get; init; } = [];

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? CityName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The audience level name. An index that ignores this leaks private content.</summary>
    public required string Visibility { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>True when the index should drop the document rather than update it.</summary>
    public bool IsDeleted { get; init; }

    public int EngagementScore { get; init; }
}
