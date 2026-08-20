namespace Diaspora.Application.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuthorizeAttribute : Attribute
{
    public string? Roles { get; init; }
    public string? Policy { get; init; }
    public string? Permission { get; init; }
}
