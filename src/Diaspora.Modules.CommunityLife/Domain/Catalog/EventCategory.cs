using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Catalog;

/// <summary>
/// A kind of event — wedding, naming ceremony, career fair, Friday prayers.
/// <para>
/// <b>A table rather than an enum, for the same reason the organization type is.</b> The brief's
/// list of examples runs to twenty-four and is explicitly a list of examples; a deployment serving
/// a community with a festival nobody on this team has heard of should be able to add it without a
/// release.
/// </para>
/// <para>
/// <b><see cref="Facets"/> is what makes the brief's search requirements answerable.</b> The brief
/// asks for "religious events", "business events", "cultural events" and "student events" as search
/// facets, and those are groupings across categories rather than categories themselves: a church
/// service, Friday prayers and a temple festival are three categories and one facet. Storing the
/// facet on the category means the search filter is an indexed column rather than a hard-coded list
/// of category keys that goes stale the moment somebody adds a twenty-fifth.
/// </para>
/// </summary>
public sealed class EventCategory : Entity<EventCategoryId>, IAuditableEntity
{
    private EventCategory(
        EventCategoryId id,
        string key,
        string name,
        string? description,
        string? iconName,
        string? groupName,
        string facets,
        int order,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        IconName = iconName;
        GroupName = groupName;
        Facets = facets;
        Order = order;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private EventCategory() { }

    /// <summary>The stable key events store. Normalized, and never changed once published.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? IconName { get; private set; }

    public string? GroupName { get; private set; }

    /// <summary>
    /// The search facets this category belongs to, comma-separated and lower-cased —
    /// <c>religious,cultural</c>. Stored as one column and matched with a contains, because the set
    /// is small, fixed-ish and read on every browse; a join table would be a second query on the
    /// hottest path in the module.
    /// </summary>
    public string Facets { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Whether events of this kind default to requiring a response. A workshop does; a Sunday
    /// service does not, and defaulting the wrong way would put a barrier between a newcomer and a
    /// church door.
    /// </summary>
    public bool DefaultsToRsvp { get; private set; } = true;

    public int EventCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether the category belongs to a named search facet.</summary>
    public bool HasFacet(string? facet) =>
        !string.IsNullOrWhiteSpace(facet)
        && Facets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(facet.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static EventCategory Create(
        EventCategoryId id,
        string? key,
        string? name,
        DateTimeOffset createdAt,
        string? description = null,
        string? iconName = null,
        string? groupName = null,
        IReadOnlyList<string>? facets = null,
        int order = 0,
        bool defaultsToRsvp = true)
    {
        var normalized = Organization.NormalizeKey(key) ?? "other";

        return new EventCategory(
            id,
            normalized,
            Clip(name, CommunityLifeLimits.MaxDisplayName) ?? normalized,
            Clip(description, CommunityLifeLimits.MaxNoteLength),
            Clip(iconName, CommunityLifeLimits.MaxDisplayName),
            Clip(groupName, CommunityLifeLimits.MaxDisplayName),
            NormalizeFacets(facets),
            Math.Max(0, order),
            createdAt)
        {
            DefaultsToRsvp = defaultsToRsvp
        };
    }

    /// <summary>Restates the presentation. The key never moves.</summary>
    public void Describe(
        string? name,
        string? description,
        string? iconName,
        string? groupName,
        IReadOnlyList<string>? facets,
        int? order,
        bool? defaultsToRsvp,
        DateTimeOffset now)
    {
        Name = Clip(name, CommunityLifeLimits.MaxDisplayName) ?? Name;
        Description = Clip(description, CommunityLifeLimits.MaxNoteLength) ?? Description;
        IconName = Clip(iconName, CommunityLifeLimits.MaxDisplayName) ?? IconName;
        GroupName = Clip(groupName, CommunityLifeLimits.MaxDisplayName) ?? GroupName;

        if (facets is { Count: > 0 })
        {
            Facets = NormalizeFacets(facets);
        }

        if (order is { } value)
        {
            Order = Math.Max(0, value);
        }

        if (defaultsToRsvp is { } rsvp)
        {
            DefaultsToRsvp = rsvp;
        }

        ModifiedAt = now;
    }

    public void Retire(DateTimeOffset now)
    {
        IsActive = false;
        ModifiedAt = now;
    }

    public void Reinstate(DateTimeOffset now)
    {
        IsActive = true;
        ModifiedAt = now;
    }

    public void AdjustCount(int delta, DateTimeOffset now)
    {
        EventCount = Math.Max(0, EventCount + delta);
        ModifiedAt = now;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static string NormalizeFacets(IReadOnlyList<string>? facets) =>
        facets is not { Count: > 0 }
            ? string.Empty
            : string.Join(
                ',',
                facets
                    .Where(facet => !string.IsNullOrWhiteSpace(facet))
                    .Select(facet => facet.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal));

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
