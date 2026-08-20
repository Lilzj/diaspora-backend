using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Catalog;

/// <summary>
/// One kind of accommodation the platform will list.
/// <para>
/// <b>A table rather than an enum, because the brief says the categories must stay
/// configurable — and because they genuinely differ by country.</b> "Duplex" means a maisonette in
/// London and a semi-detached house in Lagos; "guest house" is a category of business in Mauritius
/// and a spare room in Ireland; student halls are a regulated category in some markets and not a
/// category at all in others. An enum would freeze one market's vocabulary into the schema and
/// make every later market a migration.
/// </para>
/// <para>
/// <b>Retired rather than deleted, always.</b> Listings hold the key, not a foreign key, so a
/// retired type still renders on the properties that claimed it. Deleting the row would leave
/// several thousand listings describing themselves as nothing.
/// </para>
/// <para>
/// <b><see cref="IsShared"/> and <see cref="IsTemporary"/> are the two flags that carry
/// behaviour</b>, and they are on the type rather than inferred from its name for the reason the
/// class exists: a deployment adding "Co-living Suite" must be able to say it is shared without
/// Housing having to learn the word. Shared is what permits roommate preferences and a gender
/// preference; temporary is what a member filtering for somewhere to stay for six weeks selects.
/// </para>
/// </summary>
public sealed class AccommodationType : AggregateRoot<AccommodationTypeId>, IAuditableEntity
{
    private AccommodationType(
        AccommodationTypeId id,
        string key,
        string name,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        Name = name;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private AccommodationType() { }

    /// <summary>
    /// The stable key listings store. Normalised, unique, and never changed once published — a
    /// rename changes <see cref="Name"/> and leaves this alone, because every listing ever created
    /// points at it.
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>What a member sees. Editable by staff, and never overwritten by a seeder.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>An icon key the client resolves. A name, never a URL or an image.</summary>
    public string? IconKey { get; private set; }

    /// <summary>Whether somebody else lives there. Permits roommate matching — see the summary.</summary>
    public bool IsShared { get; private set; }

    /// <summary>Whether this is somewhere to stay rather than somewhere to live.</summary>
    public bool IsTemporary { get; private set; }

    /// <summary>Where it sorts in a picker. Lower first.</summary>
    public int Order { get; private set; }

    /// <summary>Retired types stay readable and stop being selectable.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<AccommodationType> Create(
        AccommodationTypeId id,
        string? key,
        string? name,
        DateTimeOffset createdAt,
        string? description = null,
        string? iconKey = null,
        bool isShared = false,
        bool isTemporary = false,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = HousingListing.NormalizeTypeKey(key);

        if (normalizedKey is null)
        {
            return Result.Failure<AccommodationType>(HousingErrors.AccommodationTypeInvalid);
        }

        var displayName = Clamp(name, HousingLimits.MaxDisplayName);

        if (displayName is null)
        {
            return Result.Failure<AccommodationType>(HousingErrors.AccommodationTypeInvalid);
        }

        return Result.Success(new AccommodationType(id, normalizedKey, displayName, createdAt)
        {
            Description = Clamp(description, HousingLimits.MaxNoteLength),
            IconKey = Clamp(iconKey, HousingLimits.MaxDisplayName),
            IsShared = isShared,
            IsTemporary = isTemporary,
            Order = order
        });
    }

    /// <summary>
    /// Restates the presentation and the behaviour flags. The key is deliberately not editable —
    /// see the class summary.
    /// </summary>
    public Result Update(
        string? name = null,
        string? description = null,
        string? iconKey = null,
        bool? isShared = null,
        bool? isTemporary = null,
        int? order = null)
    {
        if (name is not null)
        {
            var displayName = Clamp(name, HousingLimits.MaxDisplayName);

            if (displayName is null)
            {
                return Result.Failure(HousingErrors.AccommodationTypeInvalid);
            }

            Name = displayName;
        }

        if (description is not null)
        {
            Description = Clamp(description, HousingLimits.MaxNoteLength);
        }

        if (iconKey is not null)
        {
            IconKey = Clamp(iconKey, HousingLimits.MaxDisplayName);
        }

        if (isShared is { } shared)
        {
            IsShared = shared;
        }

        if (isTemporary is { } temporary)
        {
            IsTemporary = temporary;
        }

        if (order is { } position)
        {
            Order = position;
        }

        return Result.Success();
    }

    /// <summary>Stops the type being selectable. Existing listings keep it.</summary>
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
