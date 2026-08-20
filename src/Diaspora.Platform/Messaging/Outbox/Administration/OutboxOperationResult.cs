namespace Diaspora.Platform.Messaging.Outbox.Administration;

/// <summary>
/// What an operator action actually did.
/// <para>
/// Returns a count rather than a bare success, because every one of these actions is run during
/// an incident by somebody who needs to know whether it took. "Replayed 0 messages" and
/// "replayed 4000 messages" are different situations and a boolean reports them identically.
/// </para>
/// </summary>
/// <param name="Affected">How many rows the action touched.</param>
/// <param name="Description">A sentence suitable for an audit entry or an incident log.</param>
public sealed record OutboxOperationResult(int Affected, string Description)
{
    public static OutboxOperationResult None(string description) => new(0, description);
}
