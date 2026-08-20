namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>An action a template offers, with its route still un-substituted.</summary>
public sealed record TemplateActionResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? RouteTemplate { get; init; }

    public required string Style { get; init; }

    public required int Position { get; init; }
}
