namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>The personalization profile on the wire.</summary>
public sealed record ProfileShape
{
    public int Completeness { get; init; }

    public bool IsColdStart { get; init; }

    public int SignalCount { get; init; }

    public string? PreferredLanguage { get; init; }

    public string? PrimaryCountryCode { get; init; }

    public bool IsVerified { get; init; }

    public bool HasCompletedOnboarding { get; init; }

    public List<ProfileInterestShape> Interests { get; init; } = [];

    public DateTimeOffset? LastBuiltAt { get; init; }
}
