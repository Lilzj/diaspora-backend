using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>One skill as the profile editor sends it. Normalised into a key by the aggregate.</summary>
public sealed record CandidateSkillDraft
{
    public required string? Name { get; init; }

    public SkillProficiency Proficiency { get; init; } = SkillProficiency.Unspecified;

    public int? YearsOfExperience { get; init; }
}
