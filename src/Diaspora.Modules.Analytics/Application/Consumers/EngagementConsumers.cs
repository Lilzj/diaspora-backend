using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Analytics.Application.Ingestion;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;

namespace Diaspora.Modules.Analytics.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// ENGAGEMENT AND CONVERSION
//
// **This is where CREATOR-GAP-007 stops being a promise.** Every figure below is server-attested:
// a module committed the fact inside its own transaction and the platform delivered it. No client
// had a say, nothing was inferred, and a redelivery is deduplicated on the producing contract's own
// identifier. Saves, shares, comments, reactions, helpful votes, follows and conversions are real
// on the day this module ships, with no client release and nothing switched on.
//
// **Four consumers subscribe to B6 and one deliberately does not.** B8 relays Community's comment
// event as `GuideCommentAddedIntegrationEvent` for guide-focused consumers, and its own contract
// says so plainly: "a consumer that already listens to Community's stream should not also subscribe
// here, or it counts every comment twice." This module listens to Community's stream. The relay is
// not consumed, and an architecture test asserts it stays unconsumed.
//
// **`ContentAuthorId` is why B6's contracts are the right source.** It names whose content was
// engaged with, on every one of the four, for every content type on the platform — which is exactly
// the join an owner-scoped analytics platform needs and exactly what a per-module engagement event
// would not have given.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Records a save.</summary>
public sealed class BookmarkAddedAnalyticsConsumer
    : IntegrationEventHandler<CommunityBookmarkAddedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public BookmarkAddedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        CommunityBookmarkAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentSaved,
                Version: 1,
                SourceModule: "Community",
                integrationEvent.OccurredAt,
                ActorUserId: integrationEvent.UserId,
                ContentType: AnalyticsContentTypes.FromCommunity(integrationEvent.ContentType),
                ContentId: integrationEvent.ContentId,
                OwnerUserId: integrationEvent.ContentAuthorId,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Records a share.</summary>
public sealed class ShareRecordedAnalyticsConsumer
    : IntegrationEventHandler<CommunityShareRecordedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public ShareRecordedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        CommunityShareRecordedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentShared,
                Version: 1,
                SourceModule: "Community",
                integrationEvent.OccurredAt,
                ActorUserId: integrationEvent.SharedBy,
                ContentType: AnalyticsContentTypes.FromCommunity(integrationEvent.ContentType),
                ContentId: integrationEvent.ContentId,
                OwnerUserId: integrationEvent.ContentAuthorId,
                // Where it was shared to — a low-cardinality label the metric can break down by,
                // and never the recipient, who is a person and is not analytics.
                Qualifier: integrationEvent.Kind,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a comment.
/// <para>
/// <b>A creator's replies to their own content are not comments on it.</b> The served definition
/// says "comments left on your content by somebody other than you", and the ingestion service does
/// not apply the self-exclusion rule to engagement — so the check is here, where the definition
/// lives. Without it a creator answering every question on their guide would watch their own
/// engagement climb.
/// </para>
/// </summary>
public sealed class CommentAddedAnalyticsConsumer
    : IntegrationEventHandler<CommunityCommentAddedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public CommentAddedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        CommunityCommentAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ContentAuthorId is not null
            && integrationEvent.ContentAuthorId == integrationEvent.AuthorId)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentCommented,
                Version: 1,
                SourceModule: "Community",
                integrationEvent.OccurredAt,
                ActorUserId: integrationEvent.AuthorId,
                ContentType: AnalyticsContentTypes.FromCommunity(integrationEvent.ContentType),
                ContentId: integrationEvent.ContentId,
                OwnerUserId: integrationEvent.ContentAuthorId,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a reaction.
/// <para>
/// The reaction key travels as the qualifier, which is what lets one stored fact serve two served
/// metrics: every reaction counts towards engagement, and the <c>helpful</c> ones also count towards
/// helpful votes. Storing two rows would double the engagement figure for one tap.
/// </para>
/// </summary>
public sealed class ReactionAddedAnalyticsConsumer
    : IntegrationEventHandler<CommunityReactionAddedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public ReactionAddedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        CommunityReactionAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ContentAuthorId is not null
            && integrationEvent.ContentAuthorId == integrationEvent.UserId)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentReacted,
                Version: 1,
                SourceModule: "Community",
                integrationEvent.OccurredAt,
                ActorUserId: integrationEvent.UserId,
                ContentType: AnalyticsContentTypes.FromCommunity(integrationEvent.ContentType),
                ContentId: integrationEvent.ContentId,
                OwnerUserId: integrationEvent.ContentAuthorId,
                Qualifier: integrationEvent.ReactionKey,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a follow.
/// <para>
/// <b>Only a follow of a member counts towards a creator's followers.</b> B6's follow contract
/// covers members, organizations and providers; folding all three together would report an
/// organization's audience on its founder's personal overview.
/// </para>
/// </summary>
public sealed class FollowCreatedAnalyticsConsumer
    : IntegrationEventHandler<CommunityFollowCreatedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public FollowCreatedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        CommunityFollowCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(integrationEvent.TargetType, "Member", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.AudienceFollowed,
                Version: 1,
                SourceModule: "Community",
                integrationEvent.OccurredAt,
                ActorUserId: integrationEvent.FollowerId,
                SubjectUserId: integrationEvent.TargetId,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

// ── Conversion ──────────────────────────────────────────────────────────────────────────────────
//
// Four producers, one metric, and the served definition names all four: "an RSVP for an event, an
// application for a job, a purchase for a marketplace listing, a viewing for a housing listing".
// A conversion figure whose meaning changes per row without saying so is worse than no figure.

/// <summary>Records an RSVP as a conversion on the event.</summary>
public sealed class RsvpSubmittedAnalyticsConsumer
    : IntegrationEventHandler<RsvpSubmittedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public RsvpSubmittedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        RsvpSubmittedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // A "not going" is a response, not a conversion. Counting it would make a well-attended
        // event and a widely-declined one report the same figure.
        if (!string.Equals(integrationEvent.Status, "Going", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentConverted,
                Version: 1,
                SourceModule: "CommunityLife",
                integrationEvent.RespondedAt,
                ActorUserId: integrationEvent.AttendeeUserId,
                ContentType: AnalyticsContentType.Event,
                ContentId: integrationEvent.CommunityEventId,
                OwnerUserId: integrationEvent.OrganizerUserId,
                CommunityId: integrationEvent.OrganizationId,
                Qualifier: "rsvp",
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a job application as a conversion on the posting.
/// <para>
/// <b>The applicant is the actor and never the subject.</b> §12 of the Phase 15 register calls the
/// candidate funnel "the most sensitive in the platform": it describes real people's job
/// applications. What is stored here is one row saying somebody applied, keyed by a hash — there is
/// no applicant list, no demographic dimension, and no contract in this module that could serve one.
/// </para>
/// </summary>
public sealed class ApplicationSubmittedAnalyticsConsumer
    : IntegrationEventHandler<ApplicationSubmittedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public ApplicationSubmittedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        ApplicationSubmittedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentConverted,
                Version: 1,
                SourceModule: "Jobs",
                integrationEvent.SubmittedAt,
                ActorUserId: integrationEvent.ApplicantUserId,
                ContentType: AnalyticsContentType.Job,
                ContentId: integrationEvent.JobId,
                OwnerUserId: integrationEvent.EmployerUserId ?? integrationEvent.EmployerId,
                CountryCode: integrationEvent.CountryCode,
                Qualifier: "application",
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a sale as a conversion on the listing.
/// <para>
/// <b>The amount is not recorded, and that is the B15 boundary.</b> The contract carries one and
/// this consumer drops it: money is B15's, a revenue figure derived from a marketplace event would
/// be a second answer to what somebody earned, and §37 forbids exactly that. The conversion is the
/// analytical fact; the money is the ledger's.
/// </para>
/// </summary>
public sealed class ListingSoldAnalyticsConsumer
    : IntegrationEventHandler<ListingSoldIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public ListingSoldAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        ListingSoldIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentConverted,
                Version: 1,
                SourceModule: "Marketplace",
                integrationEvent.SoldAt,
                ActorUserId: integrationEvent.BuyerUserId,
                ContentType: AnalyticsContentType.Marketplace,
                ContentId: integrationEvent.ListingId,
                OwnerUserId: integrationEvent.SellerId,
                CountryCode: integrationEvent.CountryCode,
                Qualifier: "sale",
                Weight: Math.Max(1, integrationEvent.Quantity),
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Records a viewing as a conversion on the housing listing.</summary>
public sealed class ViewingScheduledAnalyticsConsumer
    : IntegrationEventHandler<ViewingScheduledIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public ViewingScheduledAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        ViewingScheduledIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentConverted,
                Version: 1,
                SourceModule: "Housing",
                integrationEvent.ScheduledAt,
                ActorUserId: integrationEvent.SeekerUserId,
                ContentType: AnalyticsContentType.Housing,
                ContentId: integrationEvent.ListingId,
                OwnerUserId: integrationEvent.LandlordId,
                Qualifier: "viewing",
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}
