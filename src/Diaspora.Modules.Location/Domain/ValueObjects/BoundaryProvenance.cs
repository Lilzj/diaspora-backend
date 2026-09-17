using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// Where an outline came from, under what licence, and how it must be credited.
/// <para>
/// <b>An outline is never stored without one.</b> Administrative boundaries are almost always
/// somebody else's data under somebody else's terms, and the terms travel with the geometry: the
/// OpenStreetMap outlines this catalogue imports are under the Open Database License, which
/// requires the credit to be shown wherever the data is. A boundary whose origin nobody recorded
/// can be neither credited nor corrected, and a member deciding where to rent is owed the answer
/// to "who says the town ends here?".
/// </para>
/// <para>
/// <see cref="SourceReference"/> is the source's own identifier for the shape — an OSM relation,
/// say — so a disputed line can be looked up where it was drawn rather than argued about here.
/// <see cref="RetrievedOn"/> is what lets a newer extract of the same source replace an older one
/// without ever overwriting a boundary somebody else supplied.
/// </para>
/// </summary>
public sealed class BoundaryProvenance : ValueObject
{
    public const int SourceMaxLength = 64;

    public const int LicenceMaxLength = 64;

    public const int AttributionMaxLength = 200;

    public const int SourceReferenceMaxLength = 128;

    /// <summary>The dataset, as its publisher names it: "OpenStreetMap".</summary>
    public string Source { get; }

    /// <summary>The licence, as an SPDX identifier where one exists: "ODbL-1.0".</summary>
    public string Licence { get; }

    /// <summary>The credit the licence requires, verbatim: "© OpenStreetMap contributors".</summary>
    public string Attribution { get; }

    /// <summary>The source's identifier for this shape, e.g. "relation/175342". Optional.</summary>
    public string? SourceReference { get; }

    /// <summary>The day the shape was taken from the source.</summary>
    public DateOnly RetrievedOn { get; }

    private BoundaryProvenance(
        string source, string licence, string attribution, string? sourceReference, DateOnly retrievedOn)
    {
        Source = source;
        Licence = licence;
        Attribution = attribution;
        SourceReference = sourceReference;
        RetrievedOn = retrievedOn;
    }

    public static Result<BoundaryProvenance> TryCreate(
        string? source,
        string? licence,
        string? attribution,
        string? sourceReference,
        DateOnly retrievedOn)
    {
        var missing = string.IsNullOrWhiteSpace(source) ? "source"
            : string.IsNullOrWhiteSpace(licence) ? "licence"
            : string.IsNullOrWhiteSpace(attribution) ? "attribution"
            : null;

        if (missing is not null)
        {
            return Result.Failure<BoundaryProvenance>(Error.Validation(
                LocationErrorCodes.PolygonInvalid,
                $"A boundary needs its {missing}: an outline nobody can credit cannot be published."));
        }

        var trimmedSource = source!.Trim();
        var trimmedLicence = licence!.Trim();
        var trimmedAttribution = attribution!.Trim();
        var trimmedReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();

        if (trimmedSource.Length > SourceMaxLength
            || trimmedLicence.Length > LicenceMaxLength
            || trimmedAttribution.Length > AttributionMaxLength
            || trimmedReference?.Length > SourceReferenceMaxLength)
        {
            return Result.Failure<BoundaryProvenance>(Error.Validation(
                LocationErrorCodes.PolygonInvalid,
                "A boundary's source, licence, attribution or reference is longer than the catalogue stores."));
        }

        if (retrievedOn == default)
        {
            return Result.Failure<BoundaryProvenance>(Error.Validation(
                LocationErrorCodes.PolygonInvalid,
                "A boundary needs the day it was retrieved, or a newer extract could never replace it."));
        }

        return Result.Success(new BoundaryProvenance(
            trimmedSource, trimmedLicence, trimmedAttribution, trimmedReference, retrievedOn));
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same shape from the same source, taken later —
    /// the one case in which an imported outline may replace a stored one.
    /// </summary>
    public bool IsSupersededBy(BoundaryProvenance other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(Source, other.Source, StringComparison.Ordinal)
            && string.Equals(SourceReference, other.SourceReference, StringComparison.Ordinal)
            && other.RetrievedOn > RetrievedOn;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Source;
        yield return Licence;
        yield return Attribution;
        yield return SourceReference;
        yield return RetrievedOn;
    }
}
