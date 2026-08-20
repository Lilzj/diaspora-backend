using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>One skill as the editor sends it. Normalised into a key by the aggregate.</summary>
public sealed record JobSkillDraft
{
    public required string? Name { get; init; }

    public SkillProficiency MinimumProficiency { get; init; } = SkillProficiency.Unspecified;

    /// <summary>Whether the posting treats this as a bar or a preference.</summary>
    public bool IsEssential { get; init; } = true;
}
