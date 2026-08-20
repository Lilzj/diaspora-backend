namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// One work-history entry as the profile editor sends it.
/// <para>
/// <see cref="EntryId"/> may be empty for a new entry. Preserving it on an existing one is what
/// keeps row identities stable across a whole-profile save.
/// </para>
/// </summary>
public sealed record CandidateExperienceDraft
{
    public Guid EntryId { get; init; }

    public required string? Title { get; init; }

    public required string? Organization { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public required DateOnly Start { get; init; }

    public DateOnly? End { get; init; }

    public bool IsCurrent { get; init; }

    public string? Description { get; init; }
}
