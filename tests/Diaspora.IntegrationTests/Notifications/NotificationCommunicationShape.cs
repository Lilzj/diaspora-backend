namespace Diaspora.IntegrationTests.Modules;

/// <summary>The timing and language half of the settings screen, as a client reads it.</summary>
internal sealed record CommunicationShape
{
    public bool DoNotDisturb { get; init; }

    public bool QuietHoursEnabled { get; init; }

    public int QuietHoursStart { get; init; }

    public int QuietHoursEnd { get; init; }

    public string DigestFrequency { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;

    public string TimeZoneId { get; init; } = string.Empty;

    public bool CriticalOverrideEnabled { get; init; }
}
