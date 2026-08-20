namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>A declared hole, as a contributor states it.</summary>
public sealed record TemplateVariableSeed
{
    public required string Name { get; init; }

    public bool IsRequired { get; init; } = true;

    /// <summary>One of the constants on <c>TemplateVariable</c>, or null for plain text.</summary>
    public string? Format { get; init; }

    public string? Sample { get; init; }
}
