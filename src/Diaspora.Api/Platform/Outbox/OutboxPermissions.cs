namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// The permissions behind the outbox's operator surface.
/// <para>
/// Split into reading and acting because they are genuinely different risks. Reading the queue
/// during an incident is something any on-call engineer should be able to do; replaying four
/// thousand dead letters recreates the load that produced them, and skipping a message stops a
/// committed business event from ever reaching its consumers.
/// </para>
/// </summary>
public static class OutboxPermissions
{
    /// <summary>Inspect depth, pending messages, dead letters and attempt history.</summary>
    public const string Read = "platform:outbox:read";

    /// <summary>Replay, skip, force a retry, purge delivered records.</summary>
    public const string Manage = "platform:outbox:manage";
}
