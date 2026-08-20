using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Catalog;

/// <summary>
/// One thing a property may have.
/// <para>
/// <b>Configurable for the same reason accommodation types are, and more urgently.</b> The amenity
/// that decides a letting is not the same in every market: a generator and a borehole matter in
/// Lagos, air conditioning in Port Louis, double glazing in Manchester, and none of those lists
/// belongs in a shared enum. A deployment adds an amenity as a row and every listing can claim it
/// the same day.
/// </para>
/// <para>
/// <b><see cref="Group"/> is what stops the amenity list becoming a wall of forty ticks.</b>
/// Utilities, safety, connectivity, outdoor space, accessibility — grouping is presentation, but
/// presentation is most of what an amenity list is for, and putting the group on the row means the
/// client does not maintain a second copy of the taxonomy.
/// </para>
/// <para>
/// <b><see cref="IsAccessibilityFeature"/> is called out separately, and not as a group.</b> Step
/// free access and a wheelchair-accessible bathroom are not conveniences to be listed beside a
/// swimming pool: for the members who need them they are the filter that decides whether a
/// property is habitable at all, and the flag is what lets that filter exist independently of how
/// a deployment chose to group things.
/// </para>
/// <para>
/// Retired rather than deleted, always — listings hold the key.
/// </para>
/// </summary>
public sealed class HousingAmenity : AggregateRoot<HousingAmenityId>, IAuditableEntity
{
    private HousingAmenity(
        HousingAmenityId id,
        string key,
        string name,
        string? group,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        Name = name;
        Group = group;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private HousingAmenity() { }

    /// <summary>The stable key listings store. Normalised, unique, never changed once published.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>The presentation group. See the class summary.</summary>
    public string? Group { get; private set; }

    /// <summary>An icon key the client resolves. A name, never a URL.</summary>
    public string? IconKey { get; private set; }

    /// <summary>
    /// Whether this is an accessibility feature rather than a convenience. See the class summary
    /// for why it is a flag and not a group.
    /// </summary>
    public bool IsAccessibilityFeature { get; private set; }

    /// <summary>
    /// Whether the search facets offer it. Not every amenity deserves a checkbox: a facet list of
    /// forty is a facet list nobody reads.
    /// </summary>
    public bool IsFilterable { get; private set; }

    public int Order { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<HousingAmenity> Create(
        HousingAmenityId id,
        string? key,
        string? name,
        DateTimeOffset createdAt,
        string? group = null,
        string? iconKey = null,
        bool isAccessibilityFeature = false,
        bool isFilterable = true,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = HousingAmenityAssignment.NormalizeKey(key);

        if (normalizedKey is null)
        {
            return Result.Failure<HousingAmenity>(HousingErrors.AmenityInvalid);
        }

        var displayName = Clamp(name, HousingLimits.MaxDisplayName);

        if (displayName is null)
        {
            return Result.Failure<HousingAmenity>(HousingErrors.AmenityInvalid);
        }

        return Result.Success(new HousingAmenity(
            id, normalizedKey, displayName, Clamp(group, HousingLimits.MaxDisplayName), createdAt)
        {
            IconKey = Clamp(iconKey, HousingLimits.MaxDisplayName),
            IsAccessibilityFeature = isAccessibilityFeature,
            IsFilterable = isFilterable,
            Order = order
        });
    }

    public Result Update(
        string? name = null,
        string? group = null,
        string? iconKey = null,
        bool? isAccessibilityFeature = null,
        bool? isFilterable = null,
        int? order = null)
    {
        if (name is not null)
        {
            var displayName = Clamp(name, HousingLimits.MaxDisplayName);

            if (displayName is null)
            {
                return Result.Failure(HousingErrors.AmenityInvalid);
            }

            Name = displayName;
        }

        if (group is not null)
        {
            Group = Clamp(group, HousingLimits.MaxDisplayName);
        }

        if (iconKey is not null)
        {
            IconKey = Clamp(iconKey, HousingLimits.MaxDisplayName);
        }

        if (isAccessibilityFeature is { } accessibility)
        {
            IsAccessibilityFeature = accessibility;
        }

        if (isFilterable is { } filterable)
        {
            IsFilterable = filterable;
        }

        if (order is { } position)
        {
            Order = position;
        }

        return Result.Success();
    }

    public void Retire() => IsActive = false;

    public void Reinstate() => IsActive = true;

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

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
    }
}
