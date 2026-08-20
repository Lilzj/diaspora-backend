namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Records what an existing household is looking for in a flatmate.
/// <para>
/// <b>Refused on anything that is not shared accommodation.</b> A household choosing who they share
/// a kitchen with is lawful in most of the markets this platform serves; the same restriction on a
/// whole property is housing discrimination in nearly all of them, and the aggregate enforces the
/// distinction rather than trusting the client to.
/// </para>
/// <para>
/// <b>Nationality preference is dropped rather than refused</b> on any deployment that has not
/// deliberately enabled it. A landlord should not get a validation error for a field their client
/// showed them.
/// </para>
/// </summary>
public sealed record SetRoommatePreferencesRequest
{
    /// <summary>Any, MaleOnly, FemaleOnly or FamilyOnly. Shared accommodation only.</summary>
    public string? GenderPreference { get; init; }

    /// <summary>Any, Student, Professional, Family or Retired.</summary>
    public string? Occupation { get; init; }

    public int? MinimumAge { get; init; }

    public int? MaximumAge { get; init; }

    public string? SmokingPolicy { get; init; }

    public string? PetPolicy { get; init; }

    /// <summary>Quiet, Moderate or Lively.</summary>
    public string? NoiseTolerance { get; init; }

    /// <summary>Dropped unless the deployment has enabled it. See the class summary.</summary>
    public string? NationalityPreference { get; init; }

    /// <summary>Languages spoken in the household. The field that actually helps.</summary>
    public IReadOnlyList<string>? Languages { get; init; }

    public int? BaseRevision { get; init; }
}
