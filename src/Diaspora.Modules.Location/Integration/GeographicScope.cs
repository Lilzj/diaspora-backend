using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// A resolved answer to "where is this request about?", ready for a vertical to filter with.
/// <para>
/// This is the type that saves every future module from parsing geography. A Jobs endpoint
/// receives <c>cityName</c>, <c>latitude</c>, <c>longitude</c> and <c>radiusKm</c> from a
/// client that may send any subset of them in any combination, hands them to
/// <see cref="IGeographicScopeResolver"/>, and gets back something it can put straight into a
/// <c>WHERE</c> clause. Every ambiguity — a city name that matches two cities, a radius
/// sentinel, coordinates with no city, a city with no coordinates — is resolved once here
/// rather than eight times, differently, across eight verticals.
/// </para>
/// </summary>
public sealed record GeographicScope
{
    /// <summary>The country the request is about, when it narrowed to one.</summary>
    public CountryCode? CountryCode { get; init; }

    /// <summary>The catalogue node the request resolved to, when it resolved to one.</summary>
    public AreaId? AreaId { get; init; }

    /// <summary>
    /// That node's path. A vertical storing an area id per row filters by
    /// <c>path LIKE prefix || '%'</c> and gets containment at any depth for free.
    /// </summary>
    public HierarchyPath? Path { get; init; }

    /// <summary>The city name, for verticals still filtering on the denormalised string.</summary>
    public string? CityName { get; init; }

    /// <summary>The point proximity is measured from, when the request had one.</summary>
    public GeoCoordinate? Center { get; init; }

    /// <summary>How wide the request reaches. Sentinels already translated.</summary>
    public SearchRadius Radius { get; init; } = SearchRadius.Default;

    /// <summary>The box every candidate must fall in. Null when the scope is not spatial.</summary>
    public BoundingBox? Bounds { get; init; }

    /// <summary>
    /// Whether <see cref="Bounds"/> is the viewport the client asked for rather than a box
    /// derived from a circle.
    /// <para>
    /// <b>The distinction decides whether the box filters or merely prefilters.</b> A box derived
    /// from a radius is a cheap index-friendly approximation that the haversine pass then refines,
    /// so a row inside it but outside the circle must still be dropped. A viewport <em>is</em> the
    /// question: a row inside it belongs in the answer and nothing refines it further. A vertical
    /// that treated the two the same would either return the corners of a circle or discard the
    /// corners of a rectangle.
    /// </para>
    /// </summary>
    public bool IsViewport { get; init; }

    /// <summary>
    /// The smallest catalogue subtree that contains the whole viewport.
    /// <para>
    /// <b>This is how a vertical that stores no coordinate answers a map.</b> Jobs and Marketplace
    /// file a row against an area, not against a point; a rectangle means nothing to them at all.
    /// Location resolves the rectangle to catalogue nodes once, here, and hands back the deepest
    /// path every one of those nodes shares — <c>/gb/london/</c> for a viewport over part of
    /// London, <c>/gb/</c> for one spanning two English cities. A vertical matches it as the same
    /// indexed <c>LIKE 'prefix%'</c> it already uses for <see cref="Path"/>.
    /// </para>
    /// <para>
    /// <b>Deliberately not <see cref="Path"/>.</b> A vertical that <em>does</em> store coordinates
    /// filters by the rectangle exactly, and narrowing it to a subtree as well would drop a
    /// property that is inside the viewport but filed against the city next door. Both consumers
    /// are served, and neither pays for the other's approximation.
    /// </para>
    /// <para>
    /// It is an approximation and says so: a viewport over one street of London admits every job
    /// in London, because a job in London is genuinely not at a point and the alternative is an
    /// empty screen.
    /// </para>
    /// </summary>
    public HierarchyPath? ViewportPath { get; init; }

    /// <summary>
    /// Every ancestor of <see cref="ViewportPath"/>, itself included, for the containment that
    /// runs the other way.
    /// <para>
    /// A row filed against London must still appear in a viewport over Peckham. That match cannot
    /// be written as <c>prefix.StartsWith(column)</c> — EF Core does not translate a
    /// parameter-receiver <c>StartsWith</c>, and the query compiles, reads correctly and throws
    /// the first time anybody runs it. It is an <c>IN</c> over a list the caller already has, the
    /// same shape <c>ProviderQueryFilter.AncestorHierarchyPaths</c> uses and for the same reason.
    /// </para>
    /// </summary>
    public IReadOnlyList<HierarchyPath> ViewportAncestorPaths { get; init; } = [];

    /// <summary>
    /// The validated proximity question, when the scope has a centre. A vertical that wants
    /// to run its own spatial query rather than filter a fetched set uses this.
    /// </summary>
    public NearbySearch? Search { get; init; }

    /// <summary>
    /// True when nothing narrowed the request at all — no country, no area, no point.
    /// A vertical should treat this as "everywhere" rather than as "nowhere"; returning an
    /// empty list because no location was supplied is the failure mode this flag prevents.
    /// </summary>
    public bool IsUnbounded =>
        !IsViewport && CountryCode is null && AreaId is null && Center is null && CityName is null;

    /// <summary>Whether proximity ordering and distance annotation are possible.</summary>
    public bool IsSpatial => Center is not null;

    public static GeographicScope Unbounded { get; } = new();
}
