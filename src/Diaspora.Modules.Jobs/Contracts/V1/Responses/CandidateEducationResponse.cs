namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One qualification.
/// <para>
/// <see cref="CountryCode"/> is where the award was made, and it is not a detail: recognition of
/// foreign qualifications is one of the central obstacles diaspora professionals face, and a
/// profile that records a nursing degree without recording where it was awarded has thrown away
/// the fact that determines whether it counts.
/// </para>
/// </summary>
public sealed record CandidateEducationResponse
{
    public required Guid EntryId { get; init; }

    public required string Institution { get; init; }

    /// <summary>What the certificate says.</summary>
    public string? Qualification { get; init; }

    public string? FieldOfStudy { get; init; }

    /// <summary>The comparable rung a posting's minimum is checked against.</summary>
    public required string Level { get; init; }

    public string? CountryCode { get; init; }

    public required DateOnly Start { get; init; }

    public DateOnly? End { get; init; }

    public required bool IsCurrent { get; init; }
}
