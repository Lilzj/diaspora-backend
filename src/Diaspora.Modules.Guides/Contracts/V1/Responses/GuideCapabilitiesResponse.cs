namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What this deployment of B8 can do, for a client to bootstrap from.
/// <para>
/// Exists so the editor and the filter sheet are not hard-coded in the app: the block kinds a
/// deployment renders, the sort orders it accepts, the ranking strategies its installed
/// modules registered and the ceilings the aggregate enforces are all here, and a client that
/// reads them needs no release when a deployment changes its mind.
/// </para>
/// </summary>
public sealed record GuideCapabilitiesResponse
{
    /// <summary>The <c>GuideStatus</c> names.</summary>
    public required IReadOnlyList<string> Statuses { get; init; }

    /// <summary>The <c>GuideVisibility</c> names.</summary>
    public required IReadOnlyList<string> Visibilities { get; init; }

    /// <summary>The <c>GuideBlockKind</c> names the editor may use.</summary>
    public required IReadOnlyList<string> BlockKinds { get; init; }

    /// <summary>The <c>GuideDifficulty</c> names.</summary>
    public required IReadOnlyList<string> Difficulties { get; init; }

    /// <summary>The <c>GuideMediaRole</c> names.</summary>
    public required IReadOnlyList<string> MediaRoles { get; init; }

    /// <summary>The sort keys the discovery endpoint accepts. Lower-case, frozen.</summary>
    public required IReadOnlyList<string> SortOrders { get; init; }

    /// <summary>Ranking keys registered on this deployment. Grows when a search module ships.</summary>
    public required IReadOnlyList<string> RankingKeys { get; init; }

    /// <summary>The <c>GuideVoteKind</c> names.</summary>
    public required IReadOnlyList<string> VoteKinds { get; init; }

    /// <summary>Whether any verification source is registered on this deployment.</summary>
    public required bool VerificationAvailable { get; init; }

    // ── Ceilings, so a client can validate before submitting ────────────────────────────────

    public required int MaxSections { get; init; }

    public required int MaxBlocksPerSection { get; init; }

    public required int MaxBlockLength { get; init; }

    public required int MaxTags { get; init; }

    public required int MaxMediaReferences { get; init; }

    public required int MaxProviderReferences { get; init; }

    public required int MaxSummaryLength { get; init; }

    public required int MaxPageSize { get; init; }
}
