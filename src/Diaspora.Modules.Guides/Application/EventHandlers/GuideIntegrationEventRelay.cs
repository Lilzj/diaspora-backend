using Diaspora.Contracts.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Turns B8's domain events into the published integration events other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B8's domain stays private.</b> A domain event
/// carries strongly typed identifiers and domain enums; an integration event carries flat
/// primitives, so a consumer can parse it without referencing this module. Doing the translation
/// in a single class means the two never drift, and means a consumer's contract cannot be changed
/// by a refactor inside the aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The guide is already saved by the time this
/// runs; throwing here would roll back a successful publication because a notification did not go
/// out. Failures are logged, and at-least-once delivery is the contract — consumers treat
/// <c>EventId</c> as the idempotency key.
/// </para>
/// <para>
/// <b>Publication is the one event that reads the aggregate back.</b> Its contract carries the
/// summary, the tags and the sub-country geography, because the consumers that matter — a search
/// index, a recommendation model, a notification about new knowledge in your city — need all of
/// it and would otherwise each call back into B8 to get it. The read is inline, inside the same
/// unit of work that raised the event, so it is the identity map rather than a round trip.
/// </para>
/// <para>
/// <b>B13.1 made that read the rule rather than the exception.</b> Every guide contract now
/// names the guide's author, because until it did, a consumer holding one of these events could
/// tell you a guide had been featured, verified, archived or read ten thousand times and could
/// not tell you whose guide it was — so the only person the news mattered to was the only person
/// who could not be told. The author lives on the aggregate and nowhere else, which is why the
/// resolution is here: the alternative was every consumer asking B8 the same question.
/// </para>
/// <para>
/// <b><see cref="AuthorOfAsync"/> returning null is normal.</b> A guide hard-deleted between the
/// write and the relay yields nothing, and the enrichment is nullable precisely so that outcome
/// is a quiet no-notification rather than a failed publish.
/// </para>
/// </summary>
public sealed class GuideIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IGuideRepository _guides;
    private readonly ILogger<GuideIntegrationEventRelay> _logger;

    public GuideIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IGuideRepository guides,
        ILogger<GuideIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _guides = guides;
        _logger = logger;
    }

    public Task PublishAsync(
        GuideCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new GuideCreatedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                AuthorId = domainEvent.AuthorId.Value,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                CategoryKey = domainEvent.CategoryKey,
                Language = domainEvent.Language,
                CountryCode = domainEvent.CountryCode,
                CreatedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        GuideMetadataUpdatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var guide = await _guides.GetAsync(domainEvent.GuideId, cancellationToken);

        await PublishSafelyAsync(
            new GuideUpdatedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedFields = domainEvent.ChangedFields,
                ChangedAt = domainEvent.OccurredAt,
                AuthorUserId = guide?.AuthorId.Value,

                // 21 Oct 2026. A draft edit, or one to a guide nobody else may read, is told to
                // nobody else. A guide that could not be read back is the same: false.
                IsReadableByOthers = guide?.IsReadableByOthers ?? false
            },
            cancellationToken);
    }

    /// <summary>
    /// A structural edit, relayed as the same update contract.
    /// <para>
    /// One <c>GuideUpdated</c> contract covers metadata and content alike, because to a consumer
    /// the fact is identical: this guide changed, reindex it. The changed-field list is what
    /// distinguishes them, and <c>content</c> is the field name a structure replacement reports.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        GuideContentUpdatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var guide = await _guides.GetAsync(domainEvent.GuideId, cancellationToken);

        await PublishSafelyAsync(
            new GuideUpdatedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedFields = ["content"],
                ChangedAt = domainEvent.OccurredAt,
                AuthorUserId = guide?.AuthorId.Value,
                IsReadableByOthers = guide?.IsReadableByOthers ?? false
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        GuidePublishedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var guide = await _guides.GetAsync(domainEvent.GuideId, cancellationToken);

        await PublishSafelyAsync(
            new GuidePublishedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                Slug = domainEvent.Slug,
                Title = domainEvent.Title,
                Summary = guide?.Summary?.Summary,
                CategoryKey = domainEvent.CategoryKey,
                Tags = guide is null ? [] : [.. guide.Tags.OrderBy(tag => tag.Order).Select(tag => tag.Slug)],
                CountryCode = domainEvent.CountryCode,
                RegionName = guide?.Location.RegionName,
                CityName = guide?.Location.CityName,
                Language = domainEvent.Language,
                AuthorId = domainEvent.AuthorId.Value,
                Version = domainEvent.Version,
                IsFirstPublication = domainEvent.IsFirstPublication,
                PublishedAt = domainEvent.PublishedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A new immutable version exists.
    /// <para>
    /// The version's own identifier is not on the domain event — the aggregate raises the fact
    /// before the version row is constructed — so the guide identifier stands in for it. A
    /// consumer that wants the snapshot reads it by guide and version number, which is the
    /// unique key the version table already carries.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        GuideVersionCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new GuideVersionCreatedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                VersionId = domainEvent.GuideId.Value,
                VersionNumber = domainEvent.Version,
                EditorId = domainEvent.EditorId.Value,
                RevisionNotes = null,
                CreatedAt = domainEvent.PublishedAt,

                // Not the editor. A staff member publishing somebody's revision is exactly the
                // case the two fields exist to keep apart.
                AuthorUserId = await AuthorOfAsync(domainEvent.GuideId, cancellationToken)
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        GuideArchivedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new GuideArchivedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,

                // The person, when the archive command ran: the author. Null when the
                // moderation projection archived it, which names nobody, and attributing it to
                // the wrong person would be worse than attributing it to nobody.
                ArchivedBy = domainEvent.ArchivedBy?.Value,
                ArchivedAt = domainEvent.ArchivedAt,

                // Which is precisely why the author is carried: the actor is unknowable and the
                // person owed an explanation is not.
                AuthorUserId = await AuthorOfAsync(domainEvent.GuideId, cancellationToken)
            },
            cancellationToken);
    }

    /// <summary>Back from the archive — the guide is discoverable again.</summary>
    public async Task PublishAsync(
        GuideRestoredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new GuideRestoredIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                RestoredFrom = GuideRestoreOrigins.Archive,
                RestoredBy = null,
                RestoredAt = domainEvent.RestoredAt,
                AuthorUserId = await AuthorOfAsync(domainEvent.GuideId, cancellationToken)
            },
            cancellationToken);
    }

    /// <summary>
    /// Back from a soft delete — the second restore path, onto the same contract.
    /// <para>
    /// <c>RestoredFrom</c> is what distinguishes them, and it matters to a consumer: a guide
    /// returning from the archive was always readable to its author, whereas one returning from
    /// deletion was gone from every index and has to be re-added rather than re-ranked.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        GuideUndeletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new GuideRestoredIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                RestoredFrom = GuideRestoreOrigins.Deletion,
                RestoredBy = null,
                RestoredAt = domainEvent.OccurredAt,
                AuthorUserId = await AuthorOfAsync(domainEvent.GuideId, cancellationToken)
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        GuideDeletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var guide = await _guides.GetAsync(domainEvent.GuideId, cancellationToken);

        await PublishSafelyAsync(
            new GuideDeletedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,

                // The author is on the contract because a consumer purging a member's content
                // needs to know whose it was, and the row is about to stop being readable.
                AuthorId = guide?.AuthorId.Value ?? Guid.Empty,
                DeletedBy = null,
                DeletedAt = domainEvent.DeletedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// Staff featured a guide.
    /// <para>
    /// <b>The contract B13 named as its clearest missing recipient.</b> <c>ChangedBy</c> is the
    /// editor who made the call; the person who wrote the guide is the one being congratulated,
    /// and before B13.1 the payload could not tell them apart.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        GuideFeaturedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new GuideFeaturedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                Featured = true,
                ChangedBy = domainEvent.FeaturedBy.Value,
                ChangedAt = domainEvent.FeaturedAt,
                AuthorUserId = await AuthorOfAsync(domainEvent.GuideId, cancellationToken)
            },
            cancellationToken);
    }

    /// <summary>Unfeaturing, onto the same contract with the flag cleared.</summary>
    public async Task PublishAsync(
        GuideUnfeaturedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new GuideFeaturedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                Featured = false,

                // Unfeaturing carries no actor on the domain event — it is also reachable from
                // archival and deletion, where there is no curator to name. Empty says "the
                // platform", which is honest; naming the wrong staff member would not be.
                ChangedBy = Guid.Empty,
                ChangedAt = domainEvent.OccurredAt,
                AuthorUserId = await AuthorOfAsync(domainEvent.GuideId, cancellationToken)
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        GuideVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new GuideVerificationChangedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                State = GuideWireNames.ToWire(domainEvent.CurrentState),
                VerificationId = domainEvent.VerificationId,
                Badge = domainEvent.Badge,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.ChangedAt,
                AuthorUserId = await AuthorOfAsync(domainEvent.GuideId, cancellationToken)
            },
            cancellationToken);
    }

    public Task PublishAsync(
        GuideCategoryChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new GuideCategoryChangedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                PreviousCategoryKey = domainEvent.PreviousCategoryKey,
                CurrentCategoryKey = domainEvent.CurrentCategoryKey,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        GuideLanguageChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new GuideLanguageChangedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                PreviousLanguage = domainEvent.PreviousLanguage,
                CurrentLanguage = domainEvent.CurrentLanguage,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        GuideMediaAttachedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new GuideMediaAttachedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                MediaId = domainEvent.MediaId,
                Role = GuideWireNames.ToWire(domainEvent.Role),
                AttachedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A readership milestone.
    /// <para>
    /// Published from the analytics path rather than from a domain event, because views are
    /// counters and counters raise none — a guide read ten thousand times must not produce ten
    /// thousand messages. The caller decides a milestone was crossed; this only translates it.
    /// </para>
    /// </summary>
    public async Task PublishViewMilestoneAsync(
        Guid guideId,
        long totalViews,
        DateTimeOffset milestoneAt,
        CancellationToken cancellationToken = default) =>
        await PublishSafelyAsync(
            new GuideViewMilestoneReachedIntegrationEvent
            {
                GuideId = guideId,
                TotalViews = totalViews,
                MilestoneAt = milestoneAt,
                AuthorUserId = await AuthorOfAsync(GuideId.From(guideId), cancellationToken)
            },
            cancellationToken);

    /// <summary>
    /// A reader shared a guide.
    /// <para>
    /// Published from the share handler rather than from a domain event, as the milestone is:
    /// shares are counters on the aggregate and counters raise none. The caller already holds the
    /// guide, so the author is passed rather than read back.
    /// </para>
    /// </summary>
    public Task PublishSharedAsync(
        Guid guideId,
        Guid authorUserId,
        Guid? sharedBy,
        DateTimeOffset sharedAt,
        CancellationToken cancellationToken = default) =>
        PublishSafelyAsync(
            new GuideSharedIntegrationEvent
            {
                GuideId = guideId,
                AuthorUserId = authorUserId,
                SharedBy = sharedBy,
                SharedAt = sharedAt
            },
            cancellationToken);

    /// <summary>A reader's helpfulness vote, cast or changed. See <see cref="PublishSharedAsync"/>.</summary>
    public Task PublishVoteCastAsync(
        Guid guideId,
        Guid authorUserId,
        Guid voterUserId,
        GuideVoteKind kind,
        GuideVoteKind? previousKind,
        DateTimeOffset castAt,
        CancellationToken cancellationToken = default) =>
        PublishSafelyAsync(
            new GuideVoteCastIntegrationEvent
            {
                GuideId = guideId,
                AuthorUserId = authorUserId,
                VoterUserId = voterUserId,
                Kind = GuideWireNames.ToWire(kind),
                PreviousKind = previousKind is { } previous ? GuideWireNames.ToWire(previous) : null,
                CastAt = castAt
            },
            cancellationToken);

    /// <summary>
    /// A comment landed on a guide.
    /// <para>
    /// Relayed rather than raised: Community owns the comment and publishes its own event, and
    /// this republishes the guide-shaped fact so a consumer interested in guides need not
    /// subscribe to every comment on the platform and filter.
    /// </para>
    /// </summary>
    public async Task PublishCommentAddedAsync(
        Guid guideId,
        Guid commentId,
        Guid authorId,
        DateTimeOffset addedAt,
        CancellationToken cancellationToken = default) =>
        await PublishSafelyAsync(
            new GuideCommentAddedIntegrationEvent
            {
                GuideId = guideId,
                CommentId = commentId,

                // The commenter. The next line is the other party entirely.
                AuthorId = authorId,
                AddedAt = addedAt,
                AuthorUserId = await AuthorOfAsync(GuideId.From(guideId), cancellationToken)
            },
            cancellationToken);

    /// <summary>
    /// The guide's author, or null when the guide is no longer readable.
    /// <para>
    /// <b>One read, in one place, rather than a field every domain event would have to start
    /// carrying.</b> The author is immutable for the life of a guide and sits on the aggregate,
    /// so this is a keyed read inside the unit of work that raised the event — the identity map,
    /// not a round trip, for every path that already loaded the guide to change it.
    /// </para>
    /// <para>
    /// Null is a normal answer: a guide hard-deleted between the write and this relay has no
    /// author to name, and a nullable enrichment turns that into a notification nobody gets
    /// rather than a publish that throws.
    /// </para>
    /// </summary>
    private async Task<Guid?> AuthorOfAsync(GuideId guideId, CancellationToken cancellationToken)
    {
        var guide = await _guides.GetAsync(guideId, cancellationToken);

        return guide?.AuthorId.Value;
    }

    private async Task PublishSafelyAsync<TEvent>(
        TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed publish must not roll back a successful write.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Publishing {EventType} failed. The write succeeded; the notification did not.",
                typeof(TEvent).Name);
        }
#pragma warning restore CA1031
    }
}
