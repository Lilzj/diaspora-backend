using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>One work-authorisation entry as the profile editor sends it.</summary>
public sealed record CandidateWorkAuthorizationDraft
{
    /// <summary>The country this answer is about. ISO 3166-1 alpha-2.</summary>
    public required string? CountryCode { get; init; }

    public required WorkAuthorizationKind Kind { get; init; }

    /// <summary>Ignored for citizenship and settled status, which do not lapse.</summary>
    public DateOnly? ExpiresOn { get; init; }
}
