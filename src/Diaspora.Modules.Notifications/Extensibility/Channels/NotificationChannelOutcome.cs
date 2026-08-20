namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// What a channel managed.
/// <para>
/// <b><see cref="IsPermanent"/> is the field that matters and the one an implementer most often gets
/// wrong.</b> It means "retrying can never help": the token was rejected, the address does not
/// exist, the message was refused. A timeout, a 500 and a rate limit are all temporary, and marking
/// one permanent silently retires a member's device. When in doubt, temporary — the retry ladder is
/// bounded and a wasted attempt costs nothing anybody notices.
/// </para>
/// </summary>
public sealed record NotificationChannelOutcome
{
    private NotificationChannelOutcome(
        bool succeeded,
        string? reasonCode,
        string? detail,
        bool isPermanent,
        string? providerMessageId,
        IReadOnlyList<Guid> deadEndpoints)
    {
        Succeeded = succeeded;
        ReasonCode = reasonCode;
        Detail = detail;
        IsPermanent = isPermanent;
        ProviderMessageId = providerMessageId;
        DeadEndpoints = deadEndpoints;
    }

    public bool Succeeded { get; }

    /// <summary>A stable machine code. <c>NoEndpoint</c>, <c>Rejected</c>, <c>Timeout</c>, <c>NotConfigured</c>.</summary>
    public string? ReasonCode { get; }

    /// <summary>The transport's own words, for an operator. Never shown to a member.</summary>
    public string? Detail { get; }

    /// <summary>Whether another attempt could ever succeed. See the class summary.</summary>
    public bool IsPermanent { get; }

    public string? ProviderMessageId { get; }

    /// <summary>
    /// Endpoints the provider said are gone.
    /// <para>
    /// Reported even on a successful send, because a multicast push commonly succeeds for three
    /// devices and reports the fourth as unregistered — and an endpoint nobody retires is one the
    /// platform pays to fail forever.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid> DeadEndpoints { get; }

    public static NotificationChannelOutcome Success(
        string? providerMessageId = null, IReadOnlyList<Guid>? deadEndpoints = null) =>
        new(true, null, null, false, providerMessageId, deadEndpoints ?? []);

    /// <summary>A failure worth retrying.</summary>
    public static NotificationChannelOutcome Transient(string reasonCode, string? detail = null) =>
        new(false, reasonCode, detail, false, null, []);

    /// <summary>A failure retrying cannot fix.</summary>
    public static NotificationChannelOutcome Permanent(
        string reasonCode, string? detail = null, IReadOnlyList<Guid>? deadEndpoints = null) =>
        new(false, reasonCode, detail, true, null, deadEndpoints ?? []);

    /// <summary>Nowhere to send it. Permanent for this attempt; a new endpoint makes a new message.</summary>
    public static NotificationChannelOutcome NoEndpoint() =>
        new(false, "NoEndpoint", "The recipient has no usable endpoint for this channel.", true, null, []);

    /// <summary>No provider is configured. The honest answer for SMS, and today for push and email.</summary>
    public static NotificationChannelOutcome NotConfigured() =>
        new(false, "NotConfigured", "No transport is configured for this channel.", true, null, []);
}
