namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>A declared hole in a template.</summary>
public sealed record TemplateVariableRequest
{
    public required string Name { get; init; }

    public bool IsRequired { get; init; } = true;

    /// <summary><c>date</c>, <c>time</c>, <c>datetime</c>, <c>currency</c>, <c>number</c> or null.</summary>
    public string? Format { get; init; }

    public string? Sample { get; init; }
}
