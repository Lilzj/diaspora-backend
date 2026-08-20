namespace Diaspora.Modules.CommunityLife.Extensibility.Catalog;

/// <summary>
/// A kind of organization a contributor supplies.
/// <para>
/// A flat record with no domain type in it, so an assembly contributing a type does not reference
/// this module's aggregates — which is what makes the catalogue contributable from anywhere.
/// </para>
/// </summary>
public sealed record OrganizationTypeDescriptor
{
    /// <summary>The stable key organizations store. Normalized on the way in.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>The client's own icon name. Rendered, never interpreted.</summary>
    public string? IconName { get; init; }

    /// <summary>What the picker groups it under — "Faith", "Education", "Professional".</summary>
    public string? GroupName { get; init; }

    public int Order { get; init; }

    /// <summary>Whether organizations of this kind should be prompted to verify. A hint, not a rule.</summary>
    public bool EncouragesVerification { get; init; }
}
