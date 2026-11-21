namespace Diaspora.Contracts.Location.V1;

/// <summary>
/// A country as another module reads it.
/// <para>
/// The same reasoning as <see cref="AreaDescriptor"/>: <c>Country</c> owns spoken languages, time
/// zones, a metadata aggregate and the writes that maintain them, and the four modules that looked
/// a country up on 21 November 2026 between them read <b>one field</b> — the name — plus an
/// existence check. A projection makes that the shape of the question.
/// </para>
/// </summary>
/// <param name="Code">ISO 3166-1 alpha-2.</param>
/// <param name="Name">The English name. What a report or a label renders.</param>
/// <param name="Slug">Stable and lower-cased.</param>
/// <param name="Flag">The emoji, derived from the code rather than stored.</param>
/// <param name="IsSupported">
/// Whether Diaspora operates here. <b>Listing is not operating</b>: every country is in the
/// catalogue so a member can say where they are from, and only some are places the platform runs.
/// </param>
/// <param name="IsFeatured">Surfaced above the alphabetical list on selection screens.</param>
/// <param name="Center">Null where the catalogue has no coordinate.</param>
/// <param name="Extent">The bounding box, where one is known.</param>
public sealed record CountryDescriptor(
    CountryCode Code,
    string Name,
    string Slug,
    string Flag,
    bool IsSupported,
    bool IsFeatured,
    GeoCoordinate? Center,
    BoundingBox? Extent);
