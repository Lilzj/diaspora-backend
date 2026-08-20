namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// Where a member stands on one category's email — B13.3.
/// <para>
/// <b>Distinct from <c>CategoryPreferenceResponse</c>, which says what the platform will do.</b> This
/// says what the member asked for and how, which is the record a complaint about unwanted mail is
/// answered from and which a boolean cannot hold. The two are read from the same rows and are not
/// two sources of truth: <see cref="EmailEnabled"/> here is the same value the preferences response
/// carries.
/// </para>
/// </summary>
public sealed record NotificationSubscriptionResponse
{
    public required string CategoryKey { get; init; }

    /// <summary>The human-readable label, so a confirmation page can name what was switched off.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether the platform may send email for this category at all.
    /// <para>
    /// A mandatory category always reports true, whatever is stored against it — the platform will
    /// send it, and reporting false beside a switch that does nothing is how a member concludes the
    /// switch is broken.
    /// </para>
    /// </summary>
    public required bool EmailEnabled { get; init; }

    /// <summary>Whether a member may switch it off at all. Security and moderation cannot be.</summary>
    public required bool IsMandatory { get; init; }

    /// <summary>When they asked to stop. Null while email is on.</summary>
    public DateTimeOffset? UnsubscribedAt { get; init; }

    /// <summary>
    /// How — <c>app</c>, <c>emailLink</c> or <c>bounce</c>. Null while email is on.
    /// <para>
    /// The three oblige the platform differently, which is why one field is not enough. An app switch
    /// is reversible where the member found it. A footer link was followed with no session, so they
    /// may not know where the setting lives and the confirmation has to tell them. A bounce is not a
    /// request at all — nobody asked, the address stopped accepting mail — and turning it back on
    /// without a re-verified address resumes sending to somewhere already rejecting it.
    /// </para>
    /// </summary>
    public string? UnsubscribeSource { get; init; }
}
