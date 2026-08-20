namespace Diaspora.Application.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditableAttribute : Attribute
{
    public string? Action { get; init; }
}
