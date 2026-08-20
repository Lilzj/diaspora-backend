namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>What the email transport managed.</summary>
public sealed record EmailSendResult
{
    public required bool Succeeded { get; init; }

    public string? ReasonCode { get; init; }

    public string? Detail { get; init; }

    /// <summary>
    /// Whether retrying could ever help.
    /// <para>
    /// A hard bounce is permanent; a deferral, a throttle and a timeout are not. Treating a soft
    /// bounce as permanent is how a platform stops emailing somebody whose mailbox was briefly full.
    /// </para>
    /// </summary>
    public bool IsPermanent { get; init; }

    public string? ProviderMessageId { get; init; }

    public static EmailSendResult Success(string? providerMessageId = null) =>
        new() { Succeeded = true, ProviderMessageId = providerMessageId };

    public static EmailSendResult Failure(string reasonCode, string? detail = null, bool isPermanent = false) =>
        new() { Succeeded = false, ReasonCode = reasonCode, Detail = detail, IsPermanent = isPermanent };
}
