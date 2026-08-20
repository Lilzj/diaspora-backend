using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// The names of the real-time groups, composed in exactly one place.
/// <para>
/// <b>A group name is an authorisation boundary, which is why it is not a string literal at a call
/// site.</b> Two spellings of one boundary — <c>conversation:{id}</c> here and <c>conv:{id}</c>
/// there — is a member joining a group nothing broadcasts to, or worse, a broadcast reaching a group
/// the authorizer never vetted. Composing them here means the authorizer, the hub and the notifier
/// cannot disagree about what a name means.
/// </para>
/// <para>
/// <b>B13.3 moved the composition itself into the kernel and left this type forwarding to it.</b> The
/// two ends of a channel are usually in different modules — Community Life decides who may listen to
/// an organization, the Communication Platform is what writes to one — and module order forbids
/// either from referencing the other. The name they must agree on therefore belongs to neither, and
/// lives in <see cref="RealtimeChannels"/>. Every caller of this type is unchanged; there is still
/// exactly one place a name is built.
/// </para>
/// <para>
/// <b>Every member is joined to their own user group automatically on connect</b>, and it is the one
/// group that needs no module to vouch for it. It carries notifications, inbox counters and the
/// member's own presence across their own devices.
/// </para>
/// </summary>
public static class RealtimeGroups
{
    public const string UserPrefix = RealtimeChannels.UserPrefix;

    public const string ConversationPrefix = RealtimeChannels.ConversationPrefix;

    /// <summary>One organization's group. Owned by Community Life, since B13.3.</summary>
    public const string OrganizationPrefix = RealtimeChannels.OrganizationPrefix;

    /// <summary>One community event's group. Owned by Community Life, since B13.3.</summary>
    public const string CommunityEventPrefix = RealtimeChannels.CommunityEventPrefix;

    /// <summary>The member's own group. Every one of their sockets is in it.</summary>
    public static string ForUser(UserId userId) => RealtimeChannels.ForUser(userId);

    /// <summary>
    /// A conversation's group. Joinable when the module that owns conversations says so.
    /// </summary>
    public static string ForConversation(Guid conversationId) =>
        RealtimeChannels.ForConversation(conversationId);

    /// <summary>An organization's group. Joinable when Community Life says so.</summary>
    public static string ForOrganization(Guid organizationId) =>
        RealtimeChannels.ForOrganization(organizationId);

    /// <summary>A community event's group. Joinable when Community Life says so.</summary>
    public static string ForCommunityEvent(Guid communityEventId) =>
        RealtimeChannels.ForCommunityEvent(communityEventId);

    /// <summary>Whether a name is a user group. Used by the shipped authorizer.</summary>
    public static bool IsUserGroup(string group) =>
        RealtimeChannels.HasPrefix(group, RealtimeChannels.UserPrefix);

    /// <summary>Whether a name is a conversation group.</summary>
    public static bool IsConversationGroup(string group) =>
        RealtimeChannels.HasPrefix(group, RealtimeChannels.ConversationPrefix);

    /// <summary>
    /// The member a user group belongs to, or null when the name is not one.
    /// <para>
    /// Parsed rather than trusted: a client supplies group names, and a name that merely starts with
    /// the right prefix is not a valid identifier.
    /// </para>
    /// </summary>
    public static UserId? UserOf(string group) =>
        RealtimeChannels.IdentifierOf(group, RealtimeChannels.UserPrefix) is { } parsed
            ? UserId.From(parsed)
            : null;
}
