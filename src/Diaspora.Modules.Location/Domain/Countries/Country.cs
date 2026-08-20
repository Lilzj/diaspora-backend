using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Events;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Countries;

/// <summary>
/// A sovereign state or dependent territory: the root of the geographic hierarchy and the
/// unit almost every product decision on this platform is scoped to.
/// <para>
/// A country is its own aggregate rather than an <see cref="Areas.AdministrativeArea"/> at
/// <see cref="AdministrativeLevel.Country"/>, and the reason is that countries carry
/// obligations no other rung does. They are addressed by a stable external code rather than
/// by an identifier the platform invented; they hold currency, calling code, locale and
/// timezone; they gate whether the platform operates somewhere at all. Folding them into the
/// generic node would put seven nullable columns on every neighbourhood row to serve 195
/// rows that are not neighbourhoods.
/// </para>
/// <para>
/// <b>Support is not existence.</b> The catalogue lists every country so a member can name
/// where they are from; <see cref="IsSupported"/> says whether the platform runs there. The
/// registration flow needs both facts and they are not the same one.
/// </para>
/// </summary>
public sealed class Country : AggregateRoot<CountryId>, IAuditableEntity
{
    /// <summary>Delimiter for the packed multi-value columns. See <see cref="SpokenLanguages"/>.</summary>
    private const char ListSeparator = ',';

    /// <summary>
    /// Packed rather than mapped as a collection, deliberately. EF Core can map a
    /// <c>List&lt;string&gt;</c> to a JSON column on modern providers, but the integration
    /// suite runs on SQLite and the production database is PostgreSQL, and a packed string
    /// with a converter behaves identically on both without a provider-conditional
    /// configuration. These lists are read whole and never queried into, so nothing is lost.
    /// </summary>
    private string? _spokenLanguages;

    private string? _timeZones;

    public CountryCode Code { get; private set; } = default!;

    public PlaceName Name { get; private set; } = default!;

    public LocationSlug Slug { get; private set; } = default!;

    public CountryMetadata Metadata { get; private set; } = CountryMetadata.Empty;

    /// <summary>A representative point — normally the capital. Null until geography is imported.</summary>
    public GeoCoordinate? Center { get; private set; }

    /// <summary>The country's extent, for viewport queries. Null until geography is imported.</summary>
    public BoundingBox? Extent { get; private set; }

    /// <summary>Whether the platform operates here. Listing is not operating.</summary>
    public bool IsSupported { get; private set; }

    /// <summary>Surfaced ahead of the alphabetical list on selection screens.</summary>
    public bool IsFeatured { get; private set; }

    /// <summary>A landmark photograph for the country page header. Null until sourced.</summary>
    public string? LandmarkImageUrl { get; private set; }

    /// <summary>Every language with official or widely spoken status here.</summary>
    public IReadOnlyList<string> SpokenLanguages => Unpack(_spokenLanguages);

    /// <summary>Every IANA zone the country observes. Ordered; the first is the primary.</summary>
    public IReadOnlyList<string> TimeZones => Unpack(_timeZones);

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private Country() { }

    private Country(
        CountryId id,
        CountryCode code,
        PlaceName name,
        LocationSlug slug,
        CountryMetadata metadata,
        bool isSupported) : base(id)
    {
        Code = code;
        Name = name;
        Slug = slug;
        Metadata = metadata;
        IsSupported = isSupported;
    }

    public static Country Register(
        CountryCode code,
        PlaceName name,
        CountryMetadata? metadata = null,
        bool isSupported = false)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        var country = new Country(
            CountryId.New(),
            code,
            name,
            // Slugged from the code, not the name: codes are stable and unique, names are
            // neither. "Czechia" was "Czech Republic" until 2016 and every link built from
            // the old name would have broken.
            LocationSlug.Create(code.Value),
            (metadata ?? CountryMetadata.Empty).WithDerivedLocale(code),
            isSupported);

        country.RaiseDomainEvent(new CountryRegisteredDomainEvent
        {
            CountryId = country.Id,
            Code = code.Value,
            Name = name.Value,
            IsSupported = isSupported
        });

        return country;
    }

    /// <summary>The path every area in this country hangs off.</summary>
    public HierarchyPath Path => HierarchyPath.ForRoot(Code.Value);

    /// <summary>The flag emoji, derived from the code.</summary>
    public string Flag => Code.ToFlagEmoji();

    public void Rename(PlaceName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    public void UpdateMetadata(CountryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata.WithDerivedLocale(Code);
    }

    public void SetGeography(GeoCoordinate? center, BoundingBox? extent)
    {
        Center = center;
        Extent = extent;
    }

    public void SetLandmarkImage(string? url) =>
        LandmarkImageUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();

    public void SetSpokenLanguages(IEnumerable<LanguageCode> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        _spokenLanguages = Pack(languages.Select(language => language.Value));
    }

    public void SetTimeZones(IEnumerable<TimeZoneReference> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        _timeZones = Pack(zones.Select(zone => zone.Value));
    }

    public void Feature(bool featured) => IsFeatured = featured;

    /// <summary>
    /// Opens or closes the platform in this country.
    /// <para>
    /// Raises an event on change only. Consumers of
    /// <c>CountrySupportChangedIntegrationEvent</c> invalidate cached country lists, and a
    /// no-op re-import that republished 195 events every night would make that cache useless.
    /// </para>
    /// </summary>
    public Result ChangeSupport(bool isSupported, string reason)
    {
        if (IsSupported == isSupported)
        {
            return Result.Success();
        }

        IsSupported = isSupported;

        RaiseDomainEvent(new CountrySupportChangedDomainEvent
        {
            CountryId = Id,
            Code = Code.Value,
            IsSupported = isSupported,
            Reason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim()
        });

        return Result.Success();
    }

    private static string? Pack(IEnumerable<string> values)
    {
        var packed = string.Join(
            ListSeparator,
            values.Where(value => !string.IsNullOrWhiteSpace(value))
                  .Select(value => value.Trim())
                  .Distinct(StringComparer.OrdinalIgnoreCase));

        return packed.Length == 0 ? null : packed;
    }

    private static string[] Unpack(string? packed) =>
        string.IsNullOrWhiteSpace(packed)
            ? []
            : packed.Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
}
