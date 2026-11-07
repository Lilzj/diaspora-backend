namespace Diaspora.Application.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditableAttribute : Attribute
{
    public string? Action { get; init; }

    /// <summary>
    /// Record only when the caller is platform staff. For a read that members also make about
    /// themselves: the trail is for staff reading a member's private data, not for a member
    /// reading their own (7 Nov 2026).
    /// </summary>
    public bool OnlyForStaff { get; init; }
}
