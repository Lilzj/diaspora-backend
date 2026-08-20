namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>An action a template offers.</summary>
public sealed record TemplateActionRequest
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? RouteTemplate { get; init; }

    public string Style { get; init; } = "Primary";

    public int Position { get; init; }
}
