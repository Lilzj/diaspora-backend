namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One position on a candidate's work history.
/// <para>
/// The country is present because it is load-bearing on this platform: "five years' nursing"
/// reads differently to a recruiter depending on where it was done, and a candidate's
/// international experience is the thing this platform exists to make legible.
/// </para>
/// </summary>
public sealed record CandidateExperienceResponse
{
    public required Guid EntryId { get; init; }

    public required string Title { get; init; }

    public required string Organization { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public required DateOnly Start { get; init; }

    public DateOnly? End { get; init; }

    public required bool IsCurrent { get; init; }

    public string? Description { get; init; }
}
