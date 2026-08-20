using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// What a member wants from one subject area.
/// <para>
/// <b>Per channel, not a single boolean.</b> "Tell me about jobs in the app but do not push them"
/// is the single most common thing anybody actually wants from a notification setting, and a
/// category with one on/off switch cannot express it. The mobile client's current settings screen
/// sends one boolean per category; that maps onto this as "set every channel at once", which is why
/// the update contract accepts both shapes.
/// </para>
/// <para>
/// <see cref="UseDigest"/> is the third axis: allowed, but collected rather than immediate.
/// </para>
/// </summary>
public sealed class CategoryPreference : ValueObject
{
    /// <summary>How a member came to be unsubscribed from a category's email.</summary>
    public static class Sources
    {
        /// <summary>They changed the switch in the app. The default for any in-app write.</summary>
        public const string App = "app";

        /// <summary>They followed the link in a footer, with no session — B13.3.</summary>
        public const string EmailLink = "emailLink";

        /// <summary>
        /// The address stopped accepting mail and the platform stopped sending it.
        /// <para>
        /// Recorded because a member who never touched a setting deserves to be told why their email
        /// went quiet, and "we could not reach you" is a different conversation from "you asked us
        /// not to".
        /// </para>
        /// </summary>
        public const string Bounce = "bounce";
    }

    private CategoryPreference(
        string categoryKey,
        bool inApp,
        bool push,
        bool email,
        bool useDigest,
        bool isMuted,
        DateTimeOffset? unsubscribedAt,
        string? unsubscribeSource)
    {
        CategoryKey = categoryKey;
        InApp = inApp;
        Push = push;
        Email = email;
        UseDigest = useDigest;
        IsMuted = isMuted;
        UnsubscribedAt = unsubscribedAt;
        UnsubscribeSource = unsubscribeSource;
    }

    private CategoryPreference() { }

    public string CategoryKey { get; private set; } = string.Empty;

    public bool InApp { get; private set; }

    public bool Push { get; private set; }

    public bool Email { get; private set; }

    /// <summary>Collect rather than send. Applies to Normal and Low only; higher priorities ignore it.</summary>
    public bool UseDigest { get; private set; }

    /// <summary>
    /// The member asked for nothing at all from this category.
    /// <para>
    /// Kept separate from "every channel off" because the two are different statements and only one
    /// of them survives a future channel being added: a muted category stays muted when SMS arrives,
    /// whereas a category that merely had its three known channels switched off would start speaking
    /// again on the fourth.
    /// </para>
    /// </summary>
    public bool IsMuted { get; private set; }

    /// <summary>
    /// When email for this category was last switched off, and null while it is on — B13.3.
    /// <para>
    /// <b>Kept because an unsubscribe is a legal record, not only a setting.</b> Switching
    /// <see cref="Email"/> to false says what the platform will do; this says when the member asked
    /// and, with <see cref="UnsubscribeSource"/>, how — which is the question a complaint about
    /// unwanted mail actually asks, and the one a boolean cannot answer.
    /// </para>
    /// <para>
    /// Set on the way out only. Re-subscribing clears both, because a member who has turned email
    /// back on is not unsubscribed and a screen that still said so would be describing a state they
    /// had already left.
    /// </para>
    /// </summary>
    public DateTimeOffset? UnsubscribedAt { get; private set; }

    /// <summary>
    /// How it happened — one of <see cref="Sources"/>. Null while email is on.
    /// <para>
    /// The three differ in what they oblige the platform to do next. An app switch is reversible in
    /// the app. A footer link was followed with no session, so the member may not be able to find the
    /// setting again and the confirmation screen has to say where it is. A bounce is not a request at
    /// all, and re-enabling it without a re-verified address would resume mail to somewhere that is
    /// already rejecting it.
    /// </para>
    /// </summary>
    public string? UnsubscribeSource { get; private set; }

    public static CategoryPreference Create(
        string categoryKey,
        bool inApp = true,
        bool push = true,
        bool email = false,
        bool useDigest = false,
        bool isMuted = false,
        DateTimeOffset? unsubscribedAt = null,
        string? unsubscribeSource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        var key = categoryKey.Trim().ToLowerInvariant();

        if (key.Length > NotificationLimits.MaxKeyLength)
        {
            key = key[..NotificationLimits.MaxKeyLength];
        }

        // Email on and an unsubscribe stamp is a contradiction, and it is reachable: the update
        // command rebuilds this object from a request that names channels and knows nothing about
        // unsubscribe history. Resolving it here rather than at each call site means the invariant
        // holds however the object was built.
        return email
            ? new CategoryPreference(key, inApp, push, email: true, useDigest, isMuted, null, null)
            : new CategoryPreference(
                key, inApp, push, email: false, useDigest, isMuted, unsubscribedAt, unsubscribeSource);
    }

    /// <summary>
    /// The same preference with email switched off and the reason recorded — B13.3.
    /// <para>
    /// <b>Idempotent, and it keeps the first answer.</b> A member who follows the same footer link
    /// twice — or whose mail client prefetches it, which is the common case — must not have the
    /// record rewritten to say they asked again a second later. The first request is the one that
    /// happened.
    /// </para>
    /// </summary>
    public CategoryPreference Unsubscribed(string source, DateTimeOffset at) =>
        UnsubscribedAt is not null && !Email
            ? this
            : new CategoryPreference(
                CategoryKey, InApp, Push, email: false, UseDigest, IsMuted, at, source);

    /// <summary>Whether this category allows the channel, ignoring priority overrides.</summary>
    public bool Allows(NotificationChannelKind channel)
    {
        if (IsMuted)
        {
            return false;
        }

        return channel switch
        {
            NotificationChannelKind.InApp => InApp,
            NotificationChannelKind.Push => Push,
            NotificationChannelKind.Email => Email,

            // A channel the member has never been asked about is off. Opting somebody into SMS
            // because a column did not exist yet is not a default anybody would choose.
            _ => false
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CategoryKey;
        yield return InApp;
        yield return Push;
        yield return Email;
        yield return UseDigest;
        yield return IsMuted;
        yield return UnsubscribedAt;
        yield return UnsubscribeSource;
    }
}
