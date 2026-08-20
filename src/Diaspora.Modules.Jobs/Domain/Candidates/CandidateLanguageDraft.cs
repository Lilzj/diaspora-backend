using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>One language as the profile editor sends it.</summary>
public sealed record CandidateLanguageDraft
{
    /// <summary>ISO 639-1, two letters.</summary>
    public required string? Code { get; init; }

    public LanguageProficiency Proficiency { get; init; } = LanguageProficiency.Conversational;
}
