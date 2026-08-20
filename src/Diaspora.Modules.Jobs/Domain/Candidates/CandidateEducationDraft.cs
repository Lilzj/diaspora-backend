using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>One education entry as the profile editor sends it.</summary>
public sealed record CandidateEducationDraft
{
    public Guid EntryId { get; init; }

    public required string? Institution { get; init; }

    public string? Qualification { get; init; }

    public string? FieldOfStudy { get; init; }

    public EducationLevel Level { get; init; } = EducationLevel.None;

    /// <summary>Where the award was made. Decides whether it is recognised.</summary>
    public string? CountryCode { get; init; }

    public required DateOnly Start { get; init; }

    public DateOnly? End { get; init; }

    public bool IsCurrent { get; init; }
}
