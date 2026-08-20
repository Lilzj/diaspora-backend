namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>Whether one surface is personalised for the caller.</summary>
public sealed record SurfacePreferenceRequest
{
    public required string Surface { get; init; }

    public required bool IsEnabled { get; init; }
}
