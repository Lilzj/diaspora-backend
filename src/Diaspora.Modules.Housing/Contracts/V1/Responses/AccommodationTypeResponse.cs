namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>One entry in the accommodation-type catalogue.</summary>
public sealed record AccommodationTypeResponse
{
    public required Guid Id { get; init; }

    /// <summary>The stable key listings store.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconKey { get; init; }

    /// <summary>Whether somebody else lives there. Permits roommate matching.</summary>
    public required bool IsShared { get; init; }

    /// <summary>Whether this is somewhere to stay rather than somewhere to live.</summary>
    public required bool IsTemporary { get; init; }

    public required int Order { get; init; }

    public required bool IsActive { get; init; }
}
