namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// One entry in the reason catalogue.
/// <para>
/// Served so a client builds its report modal from the server rather than from a hardcoded list
/// — which is how the client came to have two different report taxonomies in the first place.
/// </para>
/// </summary>
public sealed record ReportReasonResponse
{
    public required string Code { get; init; }

    public required string Label { get; init; }

    /// <summary>True when the reason is unactionable without a link, as conflicting is.</summary>
    public required bool RequiresEvidence { get; init; }
}
