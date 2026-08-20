namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>Reports that a member followed a notification somewhere.</summary>
public sealed record RecordNotificationClickRequest
{
    /// <summary>Which action was tapped. Null for the card itself.</summary>
    public string? ActionKey { get; init; }

    /// <summary>
    /// Where the member was — <c>InApp</c>, <c>Push</c>, <c>Email</c>.
    /// <para>
    /// Reported by the client rather than inferred, because the same message exists on several
    /// channels and the whole point of the number is which one earned the tap. An unrecognised value
    /// is recorded as the inbox rather than dropped.
    /// </para>
    /// </summary>
    public string? Channel { get; init; }
}
