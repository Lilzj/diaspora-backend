using System.Globalization;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// The value converters every Location configuration shares.
/// <para>
/// Declared once rather than inline at each use site, because the same value object appears on
/// four or five tables and an inline converter that differed in one of them — a different
/// trim, a different case fold — would produce rows that no longer join. The storage form of a
/// value object is a property of the value object, so it lives in one place.
/// </para>
/// <para>
/// Every converter is exposed as the non-generic <see cref="ValueConverter"/> rather than the
/// typed form. That is not laziness: a value object is nullable on some tables and not on
/// others — <c>PostalCode</c> is optional on an area, <c>CountryCode</c> is required on one and
/// optional on another — and the typed overload treats
/// <c>ValueConverter&lt;PostalCode, string&gt;</c> and
/// <c>ValueConverter&lt;PostalCode?, string&gt;</c> as different types. One converter per value
/// object, usable in both positions, beats two that can drift.
/// </para>
/// <para>
/// Every converter takes the parse-or-throw factory rather than the <c>Try</c> form. A value
/// that was valid when written must round-trip, and a row that will not materialise is a
/// defect worth failing loudly on rather than silently coercing.
/// </para>
/// </summary>
public static class LocationValueConverters
{
    /// <summary>Separates coordinate pairs in a packed boundary ring.</summary>
    private const char VertexSeparator = ';';

    /// <summary>Separates latitude from longitude within a pair.</summary>
    private const char OrdinateSeparator = ',';

    public static ValueConverter CountryCode { get; } =
        new ValueConverter<CountryCode, string>(
            code => code.Value, value => Domain.ValueObjects.CountryCode.Create(value));

    public static ValueConverter Slug { get; } =
        new ValueConverter<LocationSlug, string>(
            slug => slug.Value, value => LocationSlug.Create(value));

    public static ValueConverter Path { get; } =
        new ValueConverter<HierarchyPath, string>(
            path => path.Value, value => HierarchyPath.TryCreate(value).Value);

    public static ValueConverter PostalCode { get; } =
        new ValueConverter<PostalCode, string>(
            code => code.Value, value => Domain.ValueObjects.PostalCode.Create(value));

    public static ValueConverter CallingCode { get; } =
        new ValueConverter<CallingCode, string>(
            code => code.Value, value => Domain.ValueObjects.CallingCode.Create(value));

    public static ValueConverter CurrencyCode { get; } =
        new ValueConverter<CurrencyCode, string>(
            code => code.Value, value => Domain.ValueObjects.CurrencyCode.Create(value));

    public static ValueConverter LanguageCode { get; } =
        new ValueConverter<LanguageCode, string>(
            code => code.Value, value => Domain.ValueObjects.LanguageCode.Create(value));

    public static ValueConverter Locale { get; } =
        new ValueConverter<LocaleCode, string>(
            locale => locale.Value, value => LocaleCode.TryParse(value).Value);

    public static ValueConverter TimeZone { get; } =
        new ValueConverter<TimeZoneReference, string>(
            zone => zone.Value, value => TimeZoneReference.Create(value));

    /// <summary>
    /// A strongly typed identifier converter, in the non-generic form so it can serve a
    /// nullable foreign key as well as a required one.
    /// </summary>
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();

    /// <summary>
    /// Packs a boundary ring into one text column as <c>lat,lon;lat,lon;…</c>.
    /// <para>
    /// Text rather than a provider geometry type on purpose: it is the one representation
    /// PostgreSQL, SQL Server and SQLite all store identically, which is what keeps PostGIS
    /// optional rather than required. A provider with native geometry support maps the same
    /// property to its own column type in its own configuration; nothing above persistence
    /// changes.
    /// </para>
    /// <para>
    /// Invariant culture is not a detail. A decimal comma from a de-DE server would write
    /// <c>51,4742</c> into a comma-separated format, and the ring would parse back as twice as
    /// many points, all wrong.
    /// </para>
    /// </summary>
    public static ValueConverter Boundary { get; } =
        new ValueConverter<GeographicBoundary, string>(
            boundary => Pack(boundary), value => Unpack(value));

    private static string Pack(GeographicBoundary boundary) =>
        string.Join(
            VertexSeparator,
            boundary.Vertices.Select(vertex => string.Create(
                CultureInfo.InvariantCulture,
                $"{vertex.Latitude}{OrdinateSeparator}{vertex.Longitude}")));

    private static GeographicBoundary Unpack(string packed)
    {
        var vertices = packed
            .Split(VertexSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(OrdinateSeparator))
            .Where(parts => parts.Length == 2)
            .Select(parts => GeoCoordinate.Create(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture)))
            .ToList();

        return GeographicBoundary.TryCreate(vertices).Value;
    }
}
