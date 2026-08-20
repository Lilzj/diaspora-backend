namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Replaces one of a posting's three bulleted lists wholesale.
/// <para>
/// Wholesale because that is how the editor works, and because it makes the count invariant
/// trivially correct — the lines are validated against each other as a set.
/// </para>
/// </summary>
public sealed record SetJobDetailsRequest
{
    /// <summary><c>Responsibility</c>, <c>Requirement</c> or <c>Benefit</c>.</summary>
    public required string Kind { get; init; }

    public required IReadOnlyList<JobDetailLineRequest> Lines { get; init; }

    public int? BaseRevision { get; init; }
}
