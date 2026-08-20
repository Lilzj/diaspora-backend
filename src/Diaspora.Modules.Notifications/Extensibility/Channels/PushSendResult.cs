namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// What the push transport managed.
/// <para>
/// <see cref="DeadEndpoints"/> matters even on success: a multicast to four devices routinely
/// succeeds for three and reports the fourth unregistered, and an endpoint nobody retires is one the
/// platform pays to fail forever.
/// </para>
/// </summary>
public sealed record PushSendResult
{
    public required bool Succeeded { get; init; }

    public string? ReasonCode { get; init; }

    public string? Detail { get; init; }

    /// <summary>Whether retrying could ever help. See <see cref="NotificationChannelOutcome"/>.</summary>
    public bool IsPermanent { get; init; }

    public string? ProviderMessageId { get; init; }

    /// <summary>Endpoint identifiers the provider rejected as gone.</summary>
    public IReadOnlyList<Guid> DeadEndpoints { get; init; } = [];

    public static PushSendResult Success(string? providerMessageId = null) =>
        new() { Succeeded = true, ProviderMessageId = providerMessageId };

    public static PushSendResult Failure(string reasonCode, string? detail = null, bool isPermanent = false) =>
        new() { Succeeded = false, ReasonCode = reasonCode, Detail = detail, IsPermanent = isPermanent };
}
