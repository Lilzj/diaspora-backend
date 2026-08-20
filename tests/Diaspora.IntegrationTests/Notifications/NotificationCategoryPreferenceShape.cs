namespace Diaspora.IntegrationTests.Modules;

/// <summary>One category's row on the settings screen, as a client reads it.</summary>
internal sealed record CategoryPreferenceShape
{
    public string Key { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public bool Push { get; init; }

    public bool Email { get; init; }

    public bool IsMandatory { get; init; }
}
