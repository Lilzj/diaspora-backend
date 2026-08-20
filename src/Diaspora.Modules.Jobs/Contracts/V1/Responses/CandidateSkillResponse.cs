namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One skill a candidate claims. Never verified — Trust owns verification.</summary>
public sealed record CandidateSkillResponse
{
    /// <summary>The normalised match key, identical to the posting side's normalisation.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string Proficiency { get; init; }

    public int? YearsOfExperience { get; init; }
}
