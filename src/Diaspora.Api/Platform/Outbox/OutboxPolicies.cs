namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// The policy names the outbox administration endpoints reference.
/// <para>
/// Dotted names for policies, colon-separated for the permissions behind them — the same two
/// vocabularies the identity module established, kept so a reader can tell from the shape of a
/// string which layer it belongs to.
/// </para>
/// </summary>
public static class OutboxPolicies
{
    public const string Read = "platform.outbox.read";

    public const string Manage = "platform.outbox.manage";
}
