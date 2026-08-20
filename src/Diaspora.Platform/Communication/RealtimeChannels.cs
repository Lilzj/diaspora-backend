using System.Globalization;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Platform.Communication;

/// <summary>
/// Every real-time channel name on the platform, composed in exactly one place.
/// <para>
/// <b>A channel name is an authorisation boundary, which is why it is not a string literal at a call
/// site.</b> Two spellings of one boundary — <c>organization:{id}</c> in the module that broadcasts
/// and <c>org:{id}</c> in the module that authorises — is a member joining a channel nothing writes
/// to, or worse, a broadcast reaching a channel no contributor ever vetted. Neither failure produces
/// a log line.
/// </para>
/// <para>
/// <b>It is in the kernel because the two ends of a channel are usually in different modules.</b>
/// Community Life decides who may listen to <c>organization:{id}</c>; the Communication Platform is
/// what writes to it. Module order forbids either from referencing the other, so the name they must
/// agree on belongs to neither and lives here, where both already look.
/// </para>
/// <para>
/// B13's <c>RealtimeGroups</c> predates this type and forwards to it, so the names the hub, the
/// notifier and the Conversations Platform already use are unchanged and cannot drift.
/// </para>
/// </summary>
public static class RealtimeChannels
{
    /// <summary>What separates a prefix from its identifier. Never valid inside one.</summary>
    public const char Separator = ':';

    /// <summary>A member's own channel. Joined by the server on connect; needs no permission.</summary>
    public const string UserPrefix = "user:";

    /// <summary>One conversation's channel. Owned by the Conversations Platform.</summary>
    public const string ConversationPrefix = "conversation:";

    /// <summary>One organization's channel. Owned by Community Life.</summary>
    public const string OrganizationPrefix = "organization:";

    /// <summary>
    /// One community event's channel. Owned by Community Life.
    /// <para>
    /// The occurrence and the series share it: B12.1 settled that an occurrence <em>is</em> an event
    /// (ADR-021), so a member watching a repeating event is watching the occurrence they answered
    /// for, and a second prefix would be a second name for one subject.
    /// </para>
    /// </summary>
    public const string CommunityEventPrefix = "event:";

    /// <summary>The member's own channel. Every one of their sockets is in it.</summary>
    public static string ForUser(UserId userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return Compose(UserPrefix, userId.Value);
    }

    /// <summary>A conversation's channel.</summary>
    public static string ForConversation(Guid conversationId) =>
        Compose(ConversationPrefix, conversationId);

    /// <summary>An organization's channel.</summary>
    public static string ForOrganization(Guid organizationId) =>
        Compose(OrganizationPrefix, organizationId);

    /// <summary>A community event's channel.</summary>
    public static string ForCommunityEvent(Guid communityEventId) =>
        Compose(CommunityEventPrefix, communityEventId);

    /// <summary>Whether a name sits beneath a prefix.</summary>
    public static bool HasPrefix(string? channel, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return channel is not null && channel.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// The identifier beneath a prefix, or null when the name is not one.
    /// <para>
    /// <b>Parsed rather than trusted.</b> A client supplies channel names, and a string that merely
    /// starts with the right prefix is not an identifier — <c>conversation:</c> followed by anything
    /// at all satisfies a <c>StartsWith</c> check, which is why no caller is allowed to write one.
    /// The empty identifier is refused too, because it names no record and every repository would
    /// answer "not found" for it, which reads like a permission decision and is not one.
    /// </para>
    /// </summary>
    public static Guid? IdentifierOf(string? channel, string prefix)
    {
        if (!HasPrefix(channel, prefix))
        {
            return null;
        }

        var raw = channel![prefix.Length..];

        return Guid.TryParse(raw, out var parsed) && parsed != Guid.Empty ? parsed : null;
    }

    /// <summary>
    /// The prefix part of a name, separator included, or null when it has none.
    /// <para>
    /// Used by the registry to find the contributor that owns a channel. Reads to the
    /// <em>last</em> separator, so a nested family such as <c>community:poll:{id}</c> resolves to
    /// <c>community:poll:</c> rather than to <c>community:</c> — a nested name must be owned by
    /// whoever claimed the whole of it, not by whoever claimed its first word.
    /// </para>
    /// </summary>
    public static string? PrefixOf(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        var index = channel.LastIndexOf(Separator);

        return index <= 0 ? null : channel[..(index + 1)];
    }

    private static string Compose(string prefix, Guid identifier) =>
        prefix + identifier.ToString("D", CultureInfo.InvariantCulture);
}
