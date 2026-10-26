using System.Security.Cryptography;
using System.Text;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// What makes one reputation award the same award as another.
/// <para>
/// <b>Why this exists.</b> <c>ScoreAsync</c> used to deduplicate on nothing but a rule's cap and
/// cooldown, and most rules have neither. A helpful vote flipped away and back paid twice; a guide
/// re-verified paid its approval twice; an outbox message retried because a sibling handler threw
/// paid every consumer that had already saved. The header of <c>ReputationAwardConsumers.cs</c>
/// records the incident that made this concrete — one country approval, two CountryVerified rows.
/// </para>
/// <para>
/// A key names the business fact an award is for, and <c>reputation_events</c> holds it under a
/// unique index per profile. The read in <c>ReputationService</c> gives a polite "already awarded";
/// the index is what makes it true when two deliveries race on two instances.
/// </para>
/// <para>
/// The factories are the only constructors, so the shape of every key is decided here and nowhere
/// else. A caller that cannot name its fact cannot score — <c>ScoreAsync</c> takes a key without a
/// default, which is the point: forgetting to deduplicate is a compile error, not an incident.
/// </para>
/// </summary>
public sealed record ReputationAwardKey
{
    /// <summary>Matches the <c>AwardKey</c> column.</summary>
    public const int MaxLength = 200;

    private ReputationAwardKey(string value) => Value = value;

    public string Value { get; }

    /// <summary>Once per guide, ever. A new version of a published guide is not a new guide.</summary>
    public static ReputationAwardKey GuidePublished(Guid guideId) =>
        Of(ReputationActions.GuidePublished, "guide", guideId.ToString("N"));

    /// <summary>Once per guide, ever, however often its verification flickers.</summary>
    public static ReputationAwardKey GuideApproved(Guid guideId) =>
        Of(ReputationActions.GuideApproved, "guide", guideId.ToString("N"));

    /// <summary>
    /// Once per delivered update event. Only redelivery is deduplicated here — how often an
    /// author may be paid for keeping a guide current is the rule's cooldown, not the key's.
    /// </summary>
    public static ReputationAwardKey GuideUpdated(Guid integrationEventId) =>
        Of(ReputationActions.GuideUpdated, "event", integrationEventId.ToString("N"));

    /// <summary>Once per voter per guide. Changing a vote and changing it back is one vote.</summary>
    public static ReputationAwardKey HelpfulVote(Guid guideId, Guid voterUserId) =>
        Of(ReputationActions.HelpfulVoteReceived, "guide", $"{guideId:N}:{voterUserId:N}");

    /// <summary>Once per community post.</summary>
    public static ReputationAwardKey CommunityPost(Guid postId) =>
        Of(ReputationActions.UpdatePublished, "post", postId.ToString("N"));

    /// <summary>Once per comment.</summary>
    public static ReputationAwardKey Comment(Guid commentId) =>
        Of(ReputationActions.CommentWritten, "comment", commentId.ToString("N"));

    /// <summary>Once per event, service, job, home, listing, insight or volunteer assignment (REP-14).</summary>
    public static ReputationAwardKey Published(string actionType, string kind, Guid entityId) =>
        Of(actionType, kind, entityId.ToString("N"));

    /// <summary>Once per recommendation of a service.</summary>
    public static ReputationAwardKey ProviderRecommendation(Guid recommendationId) =>
        Of(ReputationActions.ProviderRecommendationReceived, "recommendation", recommendationId.ToString("N"));

    /// <summary>Once per course a member finishes.</summary>
    public static ReputationAwardKey Course(Guid courseId) =>
        Of(ReputationActions.CourseCompleted, "course", courseId.ToString("N"));

    /// <summary>Once per referral, whoever delivers the qualification and however often.</summary>
    public static ReputationAwardKey Referral(Guid referralId) =>
        Of(ReputationActions.ReferralQualified, "referral", referralId.ToString("N"));

    /// <summary>
    /// Once per reversed event (D-9): a removal delivered twice takes the award back once.
    /// </summary>
    public static ReputationAwardKey Reversal(ReputationEventId originalEventId)
    {
        ArgumentNullException.ThrowIfNull(originalEventId);

        return Of("Reversal", "event", originalEventId.Value.ToString("N"));
    }

    /// <summary>Once per member, ever. Removing a photo and adding another is not a second photo.</summary>
    public static ReputationAwardKey ProfilePhoto(Guid userId) =>
        Of(ReputationActions.ProfilePhotoAdded, "user", userId.ToString("N"));

    /// <summary>Once per post, however often its verification flickers.</summary>
    public static ReputationAwardKey UpdateVerified(Guid postId) =>
        Of(ReputationActions.UpdateVerified, "post", postId.ToString("N"));

    /// <summary>
    /// Once per confirmer per target — the same identity <c>community_contributions</c> already
    /// holds unique, so the award and the record of it cannot disagree.
    /// </summary>
    public static ReputationAwardKey Confirmation(string targetType, string targetId, Guid confirmerUserId) =>
        Of(
            ReputationActions.CommunityConfirmation,
            targetType.Trim().ToLowerInvariant(),
            $"{targetId.Trim()}:{confirmerUserId:N}");

    /// <summary>Once per verification request, for whichever action its approval pays.</summary>
    public static ReputationAwardKey Verification(string actionType, Guid verificationId) =>
        Of(actionType, "verification", verificationId.ToString("N"));

    /// <summary>Once per moderation item, for whichever penalty its decision applies.</summary>
    public static ReputationAwardKey Moderation(string actionType, Guid moderationItemId) =>
        Of(actionType, "moderation", moderationItemId.ToString("N"));

    public override string ToString() => Value;

    /// <summary>
    /// <c>{action}:{kind}:{identity}</c>, or the action and a SHA-256 of the whole when that would
    /// not fit. Truncating instead would let two long identities collide into one award.
    /// </summary>
    private static ReputationAwardKey Of(string actionType, string kind, string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);

        var value = $"{actionType}:{kind}:{identity}";

        if (value.Length <= MaxLength)
        {
            return new ReputationAwardKey(value);
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        return new ReputationAwardKey($"{actionType}:sha256:{hash}");
    }
}
