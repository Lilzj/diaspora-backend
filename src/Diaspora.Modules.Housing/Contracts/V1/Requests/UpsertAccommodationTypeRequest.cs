namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Creates or revises an accommodation type.
/// <para>
/// The key is set once and never changed: listings store it, and renaming a type changes its
/// display name while leaving every listing that claimed it intact.
/// </para>
/// </summary>
public sealed record UpsertAccommodationTypeRequest
{
    /// <summary>Required on create, ignored on update. See the class summary.</summary>
    public string? Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconKey { get; init; }

    /// <summary>Whether somebody else lives there. Permits roommate matching on listings of this type.</summary>
    public bool IsShared { get; init; }

    /// <summary>Whether this is somewhere to stay rather than somewhere to live.</summary>
    public bool IsTemporary { get; init; }

    public int Order { get; init; }

    /// <summary>Retiring a type stops it being selectable and leaves existing listings alone.</summary>
    public bool? IsActive { get; init; }
}
