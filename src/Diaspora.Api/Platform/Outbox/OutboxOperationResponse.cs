namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// What an operator action did.
/// <para>
/// A count and a sentence, rather than a bare 204. These endpoints are used under pressure by
/// somebody who needs to know whether the thing they just did took effect, and "replayed 0" and
/// "replayed 4,000" are situations a status code cannot distinguish.
/// </para>
/// </summary>
public sealed record OutboxOperationResponse
{
    public required int Affected { get; init; }

    public required string Description { get; init; }
}
