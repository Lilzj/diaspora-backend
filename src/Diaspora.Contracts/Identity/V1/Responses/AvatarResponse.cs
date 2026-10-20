namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>The member's profile photo after a change.</summary>
public sealed record AvatarResponse
{
    /// <summary>
    /// Absolute and unsigned: render it directly. Null when the member has no photo.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>The Media asset behind it, or null.</summary>
    public Guid? MediaId { get; init; }
}
