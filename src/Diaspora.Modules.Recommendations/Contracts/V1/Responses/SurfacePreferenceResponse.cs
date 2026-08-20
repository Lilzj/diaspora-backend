namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>Whether one surface is personalised. Absence means enabled — only opt-outs are recorded.</summary>
public sealed record SurfacePreferenceResponse
{
    public required string Surface { get; init; }

    public required bool IsEnabled { get; init; }
}
