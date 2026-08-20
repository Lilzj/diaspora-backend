namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>One entry in the discoverable reason catalogue.</summary>
public sealed record ReasonCatalogShape
{
    public string Code { get; init; } = string.Empty;

    public string ResourceKey { get; init; } = string.Empty;

    public string Template { get; init; } = string.Empty;
}
