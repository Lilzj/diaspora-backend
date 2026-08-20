namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The settings screen, as a client reads it.
/// <para>
/// The three flat booleans at the top are the keys the existing mobile screen already stores in
/// AsyncStorage. If these ever stop matching, the screen stops working.
/// </para>
/// </summary>
internal sealed record PreferencesShape
{
    public bool InApp { get; init; }

    public bool Push { get; init; }

    public bool Email { get; init; }

    public List<CategoryPreferenceShape> Categories { get; init; } = [];

    public CommunicationShape Communication { get; init; } = new();
}
