namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>Replaces a posting's skill set wholesale. Deduplicated on the normalised key.</summary>
public sealed record SetJobSkillsRequest
{
    public required IReadOnlyList<JobSkillRequest> Skills { get; init; }

    public int? BaseRevision { get; init; }
}
