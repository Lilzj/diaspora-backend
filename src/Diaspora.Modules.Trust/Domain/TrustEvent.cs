using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One entry on a member's trust timeline — the "Trust Journey" the client renders.
/// <para>
/// Its own aggregate, and append-only: the timeline is the member's record of how they got
/// their standing, and it must not change when the standing does. Revoking a verification
/// adds a revocation entry; it does not erase the approval that preceded it.
/// </para>
/// <para>
/// Distinct from Identity's security events, which record logins and setting changes. That
/// distinction is why the contract report marks the audit-log screen out of scope: two event
/// streams with different retention obligations should not be merged for the convenience of
/// one screen.
/// </para>
/// </summary>
public sealed class TrustEvent : AggregateRoot<TrustEventId>
{
    public const int MaxTypeLength = 64;
    public const int MaxLabelLength = 200;
    public const int MaxDescriptionLength = 1000;
    public const int MaxIconLength = 16;

    public UserId UserId { get; private set; } = default!;

    /// <summary>Machine-readable kind — "Join", "Phone", "Country", "Expert", "Appeal".</summary>
    public string Type { get; private set; } = default!;

    /// <summary>Short headline. "Residency Verified in UK".</summary>
    public string Label { get; private set; } = default!;

    public string? Description { get; private set; }

    /// <summary>
    /// The emoji the timeline renders beside the entry.
    /// <para>
    /// A presentation concern in the domain, which normally would not survive review. It is
    /// here because the client renders <c>TrustTimelineEvent.icon</c> directly, and the
    /// alternative — a client-side map from type to emoji — means a new event type shipped by
    /// the backend renders blank until the app is updated and re-released. For a timeline
    /// that gains entry kinds over time, server-chosen is the less brittle trade.
    /// </para>
    /// </summary>
    public string? Icon { get; private set; }

    /// <summary>Which country community this belongs to, when it is country-scoped.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>
    /// Whether the member may see this entry. Internal risk and investigation notes are
    /// recorded on the same timeline but never shown.
    /// </summary>
    public bool IsMemberVisible { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private TrustEvent() { }

    private TrustEvent(
        TrustEventId id,
        UserId userId,
        string type,
        string label,
        DateTimeOffset occurredAt) : base(id)
    {
        UserId = userId;
        Type = type;
        Label = label;
        OccurredAt = occurredAt;
        IsMemberVisible = true;
    }

    public static TrustEvent Record(
        UserId userId,
        string type,
        string label,
        DateTimeOffset occurredAt,
        string? description = null,
        string? icon = null,
        string? countryCode = null,
        bool memberVisible = true)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var kind = Truncate(Guard.Against.NullOrWhiteSpace(type, nameof(type)), MaxTypeLength)!;
        var headline = Truncate(Guard.Against.NullOrWhiteSpace(label, nameof(label)), MaxLabelLength)!;

        return new TrustEvent(TrustEventId.New(), userId, kind, headline, occurredAt)
        {
            Description = Truncate(description, MaxDescriptionLength),
            Icon = Truncate(icon, MaxIconLength),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            IsMemberVisible = memberVisible
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

/// <summary>
/// The timeline entry kinds Trust itself records, and the icon each carries.
/// <para>
/// Centralised so that the same event never appears with two different icons depending on
/// which handler wrote it.
/// </para>
/// </summary>
public static class TrustEventTypes
{
    public const string Joined = "Join";
    public const string VerificationSubmitted = "VerificationSubmitted";
    public const string VerificationApproved = "VerificationApproved";
    public const string VerificationRejected = "VerificationRejected";
    public const string VerificationExpired = "VerificationExpired";
    public const string VerificationRevoked = "VerificationRevoked";
    public const string Country = "Country";
    public const string Identity = "Identity";
    public const string Phone = "Phone";
    public const string BadgeAwarded = "Badge";
    public const string LevelUp = "Expert";
    public const string AppealFiled = "Appeal";
    public const string AppealResolved = "AppealResolved";
    public const string Violation = "Violation";
    public const string Reinstated = "Reinstated";

    /// <summary>The icon a given entry kind renders with.</summary>
    public static string IconFor(string type) => type switch
    {
        Joined => "👋",
        VerificationSubmitted => "📤",
        VerificationApproved => "🛡️",
        VerificationRejected => "⚠️",
        VerificationExpired => "⏳",
        VerificationRevoked => "🚫",
        Country => "🌍",
        Identity => "🆔",
        Phone => "📱",
        BadgeAwarded => "🏅",
        LevelUp => "⭐",
        AppealFiled => "📄",
        AppealResolved => "⚖️",
        Violation => "❗",
        Reinstated => "✅",
        _ => "•"
    };
}
