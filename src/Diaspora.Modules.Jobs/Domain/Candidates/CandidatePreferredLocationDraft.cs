namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>One preferred location as the profile editor sends it.</summary>
public sealed record CandidatePreferredLocationDraft
{
    public required string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>Whether the candidate would move for it, as opposed to already being there.</summary>
    public bool WillRelocate { get; init; }
}
