namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>A language a candidate speaks, and how well.</summary>
public sealed record CandidateLanguageResponse
{
    /// <summary>Lower-case ISO 639-1.</summary>
    public required string Code { get; init; }

    /// <summary><c>Basic</c> … <c>Native</c>. The rungs real job adverts use.</summary>
    public required string Proficiency { get; init; }
}
