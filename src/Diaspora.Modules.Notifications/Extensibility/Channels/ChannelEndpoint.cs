namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// One address a channel may send to.
/// <para>
/// Deliberately untyped: a push token, an email address and a future phone number are all
/// <see cref="Address"/>. The channel knows what its own addresses look like, and the dispatcher —
/// which does not — can route to all of them through one shape.
/// </para>
/// </summary>
public sealed record ChannelEndpoint
{
    /// <summary>
    /// The endpoint's identity in this module, so a failure can be attributed back to the row that
    /// owns it. Null for an address that has no registration behind it, such as an email address
    /// resolved from a directory.
    /// </summary>
    public Guid? EndpointId { get; init; }

    /// <summary>The token, address or number. Never logged.</summary>
    public required string Address { get; init; }

    /// <summary><c>Android</c>, <c>IOS</c>, <c>Web</c>, <c>Tablet</c> or null where it means nothing.</summary>
    public string? Platform { get; init; }
}
