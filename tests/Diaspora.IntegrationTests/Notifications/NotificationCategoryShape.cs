namespace Diaspora.IntegrationTests.Modules;

/// <summary>One row of the category catalogue, as a client reads it.</summary>
internal sealed record CategoryShape
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool IsMandatory { get; init; }

    public bool DigestByDefault { get; init; }

    public int Position { get; init; }
}
