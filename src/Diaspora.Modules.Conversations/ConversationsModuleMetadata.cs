namespace Diaspora.Modules.Conversations;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class ConversationsModuleMetadata
{
    public const string Name = "Conversations";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "conversations";

    /// <summary>
    /// The one place the module's public URL space is declared, asserted by an architecture test.
    /// <para>
    /// <c>/conversations</c> is the noun the client already calls — <c>ConversationHub</c> issues
    /// twenty requests beneath it and every one of them is served here. B13 explicitly refused this
    /// URL space when it took <c>/notifications</c>, on the grounds that the conversation surface
    /// belonged to somebody else. This is that somebody.
    /// </para>
    /// <para>
    /// <b>The client's base URL is <c>/v1</c> and this is <c>/api/v1</c>.</b> That gap predates this
    /// phase, applies identically to every module, and was recorded in B13's alignment report as a
    /// gateway-rewrite concern rather than a backend defect. It is restated in this phase's
    /// compatibility report so nobody rediscovers it against a hub that works.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/conversations";
}

/// <summary>
/// What a caller must hold to act.
/// <para>
/// <b>Three permissions, and the line between them is whose conversation it is.</b> Reading and
/// writing your own conversations is ordinary membership. Moderating somebody else's — reading a
/// reported thread, closing a conversation on a removed listing — is staff, because a conversation
/// is the most private surface this platform has and a permission that blurred the two would be the
/// one nobody audits.
/// </para>
/// </summary>
public static class ConversationPermissions
{
    /// <summary>Read your own conversations, their messages and their participants.</summary>
    public const string Read = "conversations:read";

    /// <summary>Open conversations, send, edit and delete your own messages, archive, mute, pin, block.</summary>
    public const string Participate = "conversations:participate";

    /// <summary>Read and close conversations you are not in. Staff only.</summary>
    public const string Moderate = "conversations:moderate";
}

/// <summary>
/// What a deployment can switch off.
/// <para>
/// Messaging is one feature rather than nine, because a deployment that offered archiving but not
/// muting would be a deployment nobody asked for. Attachments are separate: they cost storage and a
/// deployment may want text-only conversations before Media is provisioned.
/// </para>
/// </summary>
public static class ConversationFeatures
{
    public const string Use = "conversations.use";

    public const string Attachments = "conversations.attachments";
}

public static class ConversationPolicies
{
    public const string Read = "conversations.read";

    public const string Participate = "conversations.participate";

    public const string Moderate = "conversations.moderate";
}

// The telemetry source name is deliberately NOT declared here. B13 lost a day to a type called
// NotificationTelemetry existing in two namespaces, where the root-level one silently won every
// resolution because it sat closer in scope than the `using`-imported one. There is exactly one
// ConversationTelemetry in this assembly and it lives in Observability, beside the instruments.
