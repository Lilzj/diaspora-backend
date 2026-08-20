using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Integrations.Domain.Identifiers;

/// <summary>
/// One owner's connection to one provider.
/// <para>
/// <b>Its own identifier rather than (owner, provider).</b> The pair is unique and is indexed as
/// such, but a connection outlives a disconnection — a revoked row is kept so that a later
/// reconnection is a new grant with a new audit trail rather than a mutation of an old one, and a
/// natural key would have forced the opposite.
/// </para>
/// </summary>
public sealed record IntegrationConnectionId(Guid Value) : StronglyTypedId(Value)
{
    public static IntegrationConnectionId New() => new(Guid.NewGuid());

    public static IntegrationConnectionId From(Guid value) => new(value);
}

/// <summary>
/// One authorization attempt, from the redirect to the exchange.
/// <para>
/// The record that makes an OAuth callback verifiable. It holds the state value, the PKCE
/// verifier's challenge and the redirect URI that was actually approved, and it is consumed on
/// first use — which is what turns "never trust <c>state</c> without validation" into a lookup that
/// can only succeed once.
/// </para>
/// </summary>
public sealed record OAuthAuthorizationRequestId(Guid Value) : StronglyTypedId(Value)
{
    public static OAuthAuthorizationRequestId New() => new(Guid.NewGuid());

    public static OAuthAuthorizationRequestId From(Guid value) => new(value);
}

/// <summary>
/// The mapping between something Diaspora owns and the calendar entry written for it.
/// <para>
/// The whole of the duplicate-suppression design. Without it a second export of one event is a
/// second entry in somebody's calendar, and the register calls that "the most visible possible
/// failure of this integration, and one a member cannot easily undo".
/// </para>
/// </summary>
public sealed record CalendarEventLinkId(Guid Value) : StronglyTypedId(Value)
{
    public static CalendarEventLinkId New() => new(Guid.NewGuid());

    public static CalendarEventLinkId From(Guid value) => new(value);
}

/// <summary>One member's integration preferences. One row per member, created on first write.</summary>
public sealed record IntegrationSettingsId(Guid Value) : StronglyTypedId(Value)
{
    public static IntegrationSettingsId New() => new(Guid.NewGuid());

    public static IntegrationSettingsId From(Guid value) => new(value);
}

/// <summary>One synchronisation, from accepted to terminal.</summary>
public sealed record IntegrationSyncRunId(Guid Value) : StronglyTypedId(Value)
{
    public static IntegrationSyncRunId New() => new(Guid.NewGuid());

    public static IntegrationSyncRunId From(Guid value) => new(value);
}

/// <summary>One provider callback, recorded once.</summary>
public sealed record IntegrationWebhookEventId(Guid Value) : StronglyTypedId(Value)
{
    public static IntegrationWebhookEventId New() => new(Guid.NewGuid());

    public static IntegrationWebhookEventId From(Guid value) => new(value);
}
