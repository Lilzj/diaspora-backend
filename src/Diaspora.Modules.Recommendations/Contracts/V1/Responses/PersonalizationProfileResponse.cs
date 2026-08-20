namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// What the platform believes about the caller.
/// <para>
/// <b>The transparency surface of the whole module, and it deliberately holds nothing back.</b> Every
/// affinity, every observation count, every inference the ranking uses is here — because a
/// personalization platform that cannot show somebody its model of them is asking to be trusted
/// without offering anything to check.
/// </para>
/// <para>
/// Scoped to the caller by construction: the route takes no user identifier, so reading somebody
/// else's profile is unexpressible rather than merely forbidden.
/// </para>
/// </summary>
public sealed record PersonalizationProfileResponse
{
    /// <summary>0–100. What fraction of a good profile this one has.</summary>
    public required int Completeness { get; init; }

    /// <summary>True while the platform has too little to personalise from.</summary>
    public required bool IsColdStart { get; init; }

    public required int SignalCount { get; init; }

    public string? PreferredLanguage { get; init; }

    public string? PrimaryCountryCode { get; init; }

    public string? PrimaryCityName { get; init; }

    public string? PrimaryNeighborhoodName { get; init; }

    public required bool IsVerified { get; init; }

    public required bool HasCompletedOnboarding { get; init; }

    /// <summary>Strongest first.</summary>
    public required IReadOnlyList<ProfileInterestResponse> Interests { get; init; }

    public required IReadOnlyList<ProfileLocationAffinityResponse> Locations { get; init; }

    public required IReadOnlyList<ProfileActivityResponse> Activity { get; init; }

    public DateTimeOffset? LastSignalAt { get; init; }

    public DateTimeOffset? LastBuiltAt { get; init; }

    public DateTimeOffset? LastRebuiltAt { get; init; }
}
