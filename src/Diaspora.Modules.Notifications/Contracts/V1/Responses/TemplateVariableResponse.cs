namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>A declared hole in a template.</summary>
public sealed record TemplateVariableResponse
{
    public required string Name { get; init; }

    public required bool IsRequired { get; init; }

    /// <summary><c>date</c>, <c>time</c>, <c>datetime</c>, <c>currency</c>, <c>number</c> or null.</summary>
    public string? Format { get; init; }

    public string? Sample { get; init; }
}
