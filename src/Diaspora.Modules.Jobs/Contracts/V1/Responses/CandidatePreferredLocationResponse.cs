namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// Somewhere a candidate would work.
/// <para>
/// <see cref="WillRelocate"/> is what makes this useful rather than decorative: a candidate in
/// Lagos listing Berlin might already be there, or might be saying they would move for the right
/// role — opposite signals to a recruiter deciding whether their relocation package is relevant.
/// </para>
/// </summary>
public sealed record CandidatePreferredLocationResponse
{
    public required string CountryCode { get; init; }

    public string? CityName { get; init; }

    public required bool WillRelocate { get; init; }
}
