namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// What an existing household is looking for. Present only on shared accommodation.
/// <para>
/// <c>NationalityPreference</c> is absent on every deployment that has not deliberately enabled
/// it — the field is not stored there at all, rather than stored and hidden.
/// </para>
/// </summary>
public sealed record RoommatePreferencesResponse
{
    public required string GenderPreference { get; init; }

    public required string Occupation { get; init; }

    public int? MinimumAge { get; init; }

    public int? MaximumAge { get; init; }

    public required string SmokingPolicy { get; init; }

    public required string PetPolicy { get; init; }

    public required string NoiseTolerance { get; init; }

    public string? NationalityPreference { get; init; }

    /// <summary>Languages spoken in the household.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];
}
