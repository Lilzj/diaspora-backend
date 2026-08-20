namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The wire shapes B13's integration tests bind to.
/// <para>
/// <b>Re-declared by hand rather than referenced from the module.</b> Binding a contract test to the
/// module's own response records would make it pass whenever the module compiles, which is the one
/// thing a contract test must never do. These are what a client sees, written from the client's side.
/// </para>
/// </summary>
internal sealed record UnreadCountShape
{
    public int Total { get; init; }

    public Dictionary<string, int> ByCategory { get; init; } = [];

    public bool HasNew { get; init; }
}
