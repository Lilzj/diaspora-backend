namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Replaces an employer's declared hiring regions.
/// <para>
/// Declared separately from where its postings happen to be, because an employer with three open
/// roles in Germany may hire across the whole EU — and a candidate in Portugal deciding whether
/// to follow them needs the second fact, not the first.
/// </para>
/// </summary>
public sealed record SetHiringRegionsRequest
{
    public required IReadOnlyList<HiringRegionRequest> Regions { get; init; }

    public int? BaseRevision { get; init; }
}
