namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Recommends, endorses or writes a testimonial for a provider.</summary>
public sealed record RecommendProviderRequest
{
    /// <summary>The <c>RecommendationKind</c> name. Defaults to <c>Recommend</c>.</summary>
    public string? Kind { get; init; }

    /// <summary>Required for a testimonial — an empty quotation mark on a profile helps nobody.</summary>
    public string? Note { get; init; }

    /// <summary>What is being endorsed. "Immigration appeals", "West African catering".</summary>
    public string? Skill { get; init; }
}
