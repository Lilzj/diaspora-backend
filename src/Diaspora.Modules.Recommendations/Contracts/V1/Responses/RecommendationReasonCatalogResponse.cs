namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One entry in the reason catalogue.
/// <para>
/// <b>The endpoint that returns these exists so a client can ship translations for reasons it has
/// not met yet.</b> A new ranking strategy can introduce a reason code at any time; a client that
/// discovered the full set at build time would render English for it until the next release, on the
/// one line of the card whose whole job is to be trusted.
/// </para>
/// </summary>
public sealed record RecommendationReasonCatalogResponse
{
    public required string Code { get; init; }

    public required string ResourceKey { get; init; }

    /// <summary>The English template. Contains a placeholder where a detail is substituted.</summary>
    public required string Template { get; init; }
}
