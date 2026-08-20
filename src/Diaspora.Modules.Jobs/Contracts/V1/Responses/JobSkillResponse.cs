namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One skill a posting asks for.</summary>
public sealed record JobSkillResponse
{
    /// <summary>The normalised match key. What a "jobs needing this skill" link uses.</summary>
    public required string Key { get; init; }

    /// <summary>What the employer typed. What the page prints.</summary>
    public required string Name { get; init; }

    public required string MinimumProficiency { get; init; }

    /// <summary>Whether the posting treats this as a bar or a preference.</summary>
    public required bool IsEssential { get; init; }
}
