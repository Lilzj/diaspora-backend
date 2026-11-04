using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Sections;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Guides;

/// <summary>
/// A living document of procedural knowledge. The aggregate the whole module is arranged around.
/// <para>
/// <b>The guide is the draft.</b> This aggregate always holds the current working content —
/// sections, blocks, tags, references — and publication does not move that content anywhere:
/// it stamps a version number, computes the reading profile, and a handler snapshots the
/// moment into an immutable <c>GuideVersion</c>. That split is what makes "edit a published
/// guide" a normal draft edit rather than a copy-on-write dance, and it is why readers of
/// version N are untouched by an author mid-rewrite.
/// </para>
/// <para>
/// <b>What it owns:</b> everything whose lifetime is exactly the guide's and whose consistency
/// has to hold in one transaction — its structure, its tags, its media references, its provider
/// references and its moderation trail. <b>What it deliberately does not own:</b> versions,
/// votes, suggestions, analytics and draft snapshots are separate aggregates keyed by
/// <see cref="GuideId"/>, because a guide with ten thousand votes must not be loaded to cast
/// the ten-thousand-and-first. Comments, reactions, bookmarks and reports are not here at all —
/// they belong to Community, reached through the content-source seam.
/// </para>
/// <para>
/// <b>Concurrency is a revision number, not a timestamp.</b> Every content mutation bumps
/// <see cref="Revision"/>; an editor sends back the revision it loaded and
/// <see cref="EnsureRevision"/> rejects a stale save with a conflict the client can merge from.
/// Counter mutations deliberately do not bump it — a reader viewing the guide is not an edit,
/// and must never make an author's in-flight save fail.
/// </para>
/// <para>
/// <b>Verification is a projection.</b> Nothing on this aggregate can grant a trust mark.
/// <see cref="SubmitForVerification"/> records that a claim went to Trust;
/// <see cref="ApplyVerificationDecision"/> is reachable only from a handler for a Trust
/// integration event, and is guarded on the decision timestamp because delivery is
/// at-least-once and unordered.
/// </para>
/// </summary>
public sealed class Guide
    : AggregateRoot<GuideId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private readonly List<GuideSection> _sections = [];
    private readonly List<GuideTagAssignment> _tags = [];
    private readonly List<GuideMediaReference> _mediaReferences = [];
    private readonly List<GuideProviderReference> _providerReferences = [];
    private readonly List<GuideModerationNote> _moderationNotes = [];

    private Guide(
        GuideId id,
        UserId authorId,
        GuideTitle title,
        GuideSlug slug,
        string categoryKey,
        GuideLanguage language,
        GuideLocationContext location,
        DateTimeOffset createdAt)
        : base(id)
    {
        AuthorId = authorId;
        Title = title;
        Slug = slug;
        CategoryKey = categoryKey;
        Language = language;
        Location = location;
        TranslationGroupId = id.Value;
        Status = GuideStatus.Draft;
        Visibility = GuideVisibility.Public;
        ModerationState = GuideModerationState.Clear;
        Seo = GuideSeoMetadata.Empty;
        Reading = GuideReadingProfile.Default;
        TrustMark = GuideTrustMark.None;
        Engagement = GuideEngagement.None;
        CreatedAt = createdAt;
    }

    private Guide() { }

    // ── Identity & authorship ───────────────────────────────────────────────────────────────

    /// <summary>Identity's user identifier. A value, not a foreign key.</summary>
    public UserId AuthorId { get; private set; } = default!;

    /// <summary>The platform's ownership seam. A guide is owned by its author.</summary>
    public Guid OwnerId => AuthorId.Value;

    public GuideTitle Title { get; private set; } = default!;

    /// <summary>The stable handle a deep link uses. Allocated once and never recomputed.</summary>
    public GuideSlug Slug { get; private set; } = default!;

    public GuideSummary? Summary { get; private set; }

    /// <summary>The catalogue key, validated against the category catalog by the handler.</summary>
    public string CategoryKey { get; private set; } = string.Empty;

    public GuideLanguage Language { get; private set; } = default!;

    /// <summary>
    /// Guides sharing this value are translations of each other. Defaults to the guide's own
    /// id, so "the group" always exists and linking a translation is joining, not creating.
    /// </summary>
    public Guid TranslationGroupId { get; private set; }

    public GuideLocationContext Location { get; private set; } = default!;

    /// <summary>
    /// The pin the owner dropped, in WGS84 degrees. Both or neither. Stored since 14 Oct 2026 so
    /// this can be found by coordinates once the place catalogue is retired; nothing reads it yet.
    /// </summary>
    public double? Latitude { get; private set; }

    /// <inheritdoc cref="Latitude"/>
    public double? Longitude { get; private set; }

    // ── Lifecycle & audience ────────────────────────────────────────────────────────────────

    public GuideStatus Status { get; private set; }

    public GuideVisibility Visibility { get; private set; }

    public GuideModerationState ModerationState { get; private set; }

    /// <summary>Staff-set placement. Never something an author can grant themselves.</summary>
    public bool IsFeatured { get; private set; }

    public DateTimeOffset? FeaturedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public DateTimeOffset? ScheduledPublishAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    // ── Denormalised blocks ─────────────────────────────────────────────────────────────────

    public GuideSeoMetadata Seo { get; private set; } = default!;

    public GuideReadingProfile Reading { get; private set; } = default!;

    public GuideTrustMark TrustMark { get; private set; } = default!;

    public GuideEngagement Engagement { get; private set; } = default!;

    // ── Versioning & concurrency ────────────────────────────────────────────────────────────

    /// <summary>The published version number. Zero until the first publication.</summary>
    public int CurrentVersion { get; private set; }

    /// <summary>The optimistic concurrency token. Bumped by every content mutation.</summary>
    public int Revision { get; private set; }

    // ── Owned collections ───────────────────────────────────────────────────────────────────

    public IReadOnlyList<GuideSection> Sections =>
        _sections.OrderBy(section => section.Order).ToList().AsReadOnly();

    public IReadOnlyList<GuideTagAssignment> Tags =>
        _tags.OrderBy(tag => tag.Order).ToList().AsReadOnly();

    public IReadOnlyList<GuideMediaReference> MediaReferences =>
        _mediaReferences.OrderBy(reference => reference.Order).ToList().AsReadOnly();

    public IReadOnlyList<GuideProviderReference> ProviderReferences =>
        _providerReferences.OrderBy(reference => reference.Order).ToList().AsReadOnly();

    public IReadOnlyList<GuideModerationNote> ModerationNotes =>
        _moderationNotes.OrderBy(note => note.CreatedAt).ToList().AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The bar for publication: a title, a category, and one section a reader would see.
    /// Deliberately low — an author who cannot clear it simply does not publish, and a
    /// knowledge base with fewer real guides in it helps nobody.
    /// </summary>
    public bool IsPublishable =>
        CategoryKey.Length > 0 && _sections.Exists(section => section.HasContent);

    /// <summary>Whether the guide may appear in discovery, search and nearby results.</summary>
    public bool IsDiscoverable =>
        !IsDeleted
        && Status == GuideStatus.Published
        && Visibility == GuideVisibility.Public
        && ModerationState != GuideModerationState.Hidden;

    /// <summary>
    /// Whether anybody but its author (and staff) may open the guide — 21 Oct 2026.
    /// <para>
    /// Published, Public or Unlisted (an unlisted guide is readable by whoever has its link, and a
    /// member who saved it evidently had it), not hidden by moderation, not deleted. Wider than
    /// <see cref="IsDiscoverable"/>, which also excludes Unlisted. The reader-side rule
    /// <c>GuideAuthorizationService.CanSee</c> applies, written once here so the contract that tells
    /// savers about an edit cannot drift from who may read the result.
    /// </para>
    /// </summary>
    public bool IsReadableByOthers =>
        !IsDeleted
        && Status == GuideStatus.Published
        && Visibility is GuideVisibility.Public or GuideVisibility.Unlisted
        && ModerationState != GuideModerationState.Hidden;

    /// <summary>The words a reader reads across the whole draft. Feeds the reading time.</summary>
    public int WordCount => _sections.Sum(section => section.WordCount);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a guide as a draft, visible only to its author.
    /// <para>
    /// A draft rather than a live document, deliberately: knowledge is written over sessions,
    /// and publishing straight from creation would put half-written procedures into the answers
    /// members act on. Ids and clock are passed in — the aggregate never generates either.
    /// </para>
    /// </summary>
    public static Result<Guide> Create(
        GuideId id,
        UserId authorId,
        GuideTitle title,
        GuideSlug slug,
        string? categoryKey,
        GuideLanguage language,
        GuideLocationContext location,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(authorId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(location);

        var key = NormalizeCategoryKey(categoryKey);

        if (key is null)
        {
            return Result.Failure<Guide>(GuideErrors.CategoryKeyInvalid);
        }

        var guide = new Guide(id, authorId, title, slug, key, language, location, now);

        guide.RaiseDomainEvent(new GuideCreatedDomainEvent
        {
            GuideId = id,
            AuthorId = authorId,
            Title = title.Value,
            Slug = slug.Value,
            CategoryKey = key,
            Language = language.Code,
            CountryCode = location.CountryCode
        });

        return Result.Success(guide);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rejects a save based on a revision the editor no longer holds.
    /// <para>
    /// Null means the caller opted out — an internal write, or a client that genuinely wants
    /// last-writer-wins. A conflict is a <see cref="SharedKernel.Results.ErrorType.Conflict"/>
    /// so the client can offer a merge rather than an error page.
    /// </para>
    /// </summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(GuideErrors.RevisionConflict)
            : Result.Success();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Pins this to a point. Both or neither: half a coordinate is a point on the equator or the
    /// prime meridian, not a missing value. Passing neither clears the pin.
    /// </summary>
    public Result SetCoordinates(double? latitude, double? longitude)
    {
        if ((latitude is null) != (longitude is null)
            || (latitude is { } lat && (double.IsNaN(lat) || lat is < -90d or > 90d))
            || (longitude is { } lon && (double.IsNaN(lon) || lon is < -180d or > 180d)))
        {
            return Result.Failure(GuideErrors.CoordinatesInvalid);
        }

        Latitude = latitude;
        Longitude = longitude;

        return Result.Success();
    }

    /// <summary>Updates everything about the guide that is not its structure.</summary>
    public Result UpdateMetadata(
        GuideTitle title,
        GuideSummary? summary,
        GuideSeoMetadata seo,
        GuideVisibility visibility,
        GuideDifficulty difficulty,
        int? estimatedCompletionMinutes,
        GuideLocationContext location,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(seo);
        ArgumentNullException.ThrowIfNull(location);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reading = GuideReadingProfile.Create(
            difficulty, Reading.ReadingTimeMinutes, estimatedCompletionMinutes);

        if (reading.IsFailure)
        {
            return Result.Failure(reading.Error);
        }

        var changed = new List<string>();

        if (!Title.Equals(title))
        {
            Title = title;
            changed.Add("title");
        }

        if (!Equals(Summary, summary))
        {
            Summary = summary;
            changed.Add("summary");
        }

        if (!Seo.Equals(seo))
        {
            Seo = seo;
            changed.Add("seo");
        }

        if (Visibility != visibility)
        {
            Visibility = visibility;
            changed.Add("visibility");
        }

        if (!Reading.Equals(reading.Value))
        {
            Reading = reading.Value;
            changed.Add("reading");
        }

        if (!Location.Equals(location))
        {
            Location = location;
            changed.Add("location");
        }

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        Touch();

        RaiseDomainEvent(new GuideMetadataUpdatedDomainEvent
        {
            GuideId = Id,
            ChangedBy = changedBy,
            ChangedFields = changed
        });

        return Result.Success();
    }

    /// <summary>
    /// Replaces the whole document structure — the full-document editor save.
    /// <para>
    /// Wholesale rather than per-block, because that is how the editor works: a person writes,
    /// the client saves the document, and a per-block API would make every save a diff protocol
    /// with a partial-failure story. Wholesale is also what makes the count and identity
    /// invariants trivially correct — the sections are validated against each other as a set.
    /// </para>
    /// </summary>
    public Result ReplaceStructure(
        IReadOnlyList<GuideSectionDraft> sections, int? baseRevision, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(sections);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var applied = ApplyStructure(sections);

        if (applied.IsFailure)
        {
            return applied;
        }

        Touch();
        RaiseContentUpdated(changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the tag set wholesale — that is how the client's chip editor saves, and a
    /// per-tag API would make the common case several round trips.
    /// </summary>
    public Result SetTags(IReadOnlyList<string?> tags, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in tags)
        {
            var slug = GuideTagAssignment.NormalizeSlug(raw);

            if (slug is null)
            {
                return Result.Failure(GuideErrors.TagInvalid);
            }

            if (seen.Add(slug))
            {
                resolved.Add(slug);
            }
        }

        if (resolved.Count > GuideLimits.MaxTags)
        {
            return Result.Failure(GuideErrors.TagLimit);
        }

        _tags.Clear();
        _tags.AddRange(resolved.Select(GuideTagAssignment.Create));

        Touch();

        RaiseDomainEvent(new GuideTagsChangedDomainEvent
        {
            GuideId = Id,
            Tags = resolved
        });

        return Result.Success();
    }

    /// <summary>Moves the guide to another catalogue category.</summary>
    public Result ChangeCategory(string? categoryKey, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var key = NormalizeCategoryKey(categoryKey);

        if (key is null)
        {
            return Result.Failure(GuideErrors.CategoryKeyInvalid);
        }

        if (string.Equals(CategoryKey, key, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var previous = CategoryKey;
        CategoryKey = key;
        Touch();

        RaiseDomainEvent(new GuideCategoryChangedDomainEvent
        {
            GuideId = Id,
            PreviousCategoryKey = previous,
            CurrentCategoryKey = key
        });

        return Result.Success();
    }

    /// <summary>Corrects the language the guide is written in.</summary>
    public Result ChangeLanguage(GuideLanguage language, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(language);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (Language.Equals(language))
        {
            return Result.Success();
        }

        var previous = Language;
        Language = language;
        Touch();

        RaiseDomainEvent(new GuideLanguageChangedDomainEvent
        {
            GuideId = Id,
            PreviousLanguage = previous.Code,
            CurrentLanguage = language.Code
        });

        return Result.Success();
    }

    /// <summary>
    /// Joins a translation group. Joining, never creating — the group identifier is some
    /// guide's own id, so the group exists exactly as long as any member does.
    /// </summary>
    public Result LinkTranslation(Guid translationGroupId)
    {
        if (translationGroupId == Guid.Empty)
        {
            return Result.Failure(GuideErrors.TranslationSelf);
        }

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (TranslationGroupId == translationGroupId)
        {
            return Result.Success();
        }

        TranslationGroupId = translationGroupId;
        Touch();

        return Result.Success();
    }

    /// <summary>
    /// Applies a version's content back onto the draft. The restore handler calls this;
    /// publishing afterwards is what creates the new version — a restore is an edit like any
    /// other, not time travel for readers.
    /// </summary>
    public Result RestoreVersion(
        GuideTitle title,
        GuideSummary? summary,
        GuideLanguage language,
        string? categoryKey,
        IReadOnlyList<GuideSectionDraft> sections,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(sections);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var key = NormalizeCategoryKey(categoryKey);

        if (key is null)
        {
            return Result.Failure(GuideErrors.CategoryKeyInvalid);
        }

        var applied = ApplyStructure(sections);

        if (applied.IsFailure)
        {
            return applied;
        }

        Title = title;
        Summary = summary;
        Language = language;
        CategoryKey = key;

        Touch();
        RaiseContentUpdated(changedBy);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Media & provider references
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attaches a media asset by reference. Idempotent on (media, role), which is what makes
    /// the attach endpoint safely retryable.
    /// </summary>
    public Result AttachMedia(Guid mediaId, GuideMediaRole role, string? caption = null)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (mediaId == Guid.Empty)
        {
            return Result.Failure(GuideErrors.MediaInvalid);
        }

        if (_mediaReferences.Exists(existing =>
                existing.MediaId == mediaId && existing.Role == role))
        {
            return Result.Success();
        }

        if (_mediaReferences.Count >= GuideLimits.MaxMediaReferences)
        {
            return Result.Failure(GuideErrors.MediaLimit);
        }

        var order = _mediaReferences.Count(existing => existing.Role == role);
        _mediaReferences.Add(GuideMediaReference.Create(mediaId, role, order, caption));

        Touch();

        RaiseDomainEvent(new GuideMediaAttachedDomainEvent
        {
            GuideId = Id,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Detaches a media asset. Idempotent — detaching what is not there succeeds.</summary>
    public Result DetachMedia(Guid mediaId, GuideMediaRole role)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _mediaReferences.Find(existing =>
            existing.MediaId == mediaId && existing.Role == role);

        if (reference is null)
        {
            return Result.Success();
        }

        _mediaReferences.Remove(reference);
        Touch();

        RaiseDomainEvent(new GuideMediaDetachedDomainEvent
        {
            GuideId = Id,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Points readers at a provider. Idempotent on the provider id.</summary>
    public Result AddProviderReference(Guid providerId, string? role, string? note)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (providerId == Guid.Empty)
        {
            return Result.Failure(GuideErrors.ProviderReferenceInvalid);
        }

        if (_providerReferences.Exists(existing => existing.ProviderId == providerId))
        {
            return Result.Success();
        }

        if (_providerReferences.Count >= GuideLimits.MaxProviderReferences)
        {
            return Result.Failure(GuideErrors.ProviderReferenceLimit);
        }

        _providerReferences.Add(
            GuideProviderReference.Create(providerId, role, note, _providerReferences.Count));

        Touch();

        RaiseDomainEvent(new GuideProviderReferencedDomainEvent
        {
            GuideId = Id,
            ProviderId = providerId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Removes a provider reference. Idempotent.</summary>
    public Result RemoveProviderReference(Guid providerId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _providerReferences.Find(existing => existing.ProviderId == providerId);

        if (reference is null)
        {
            return Result.Success();
        }

        _providerReferences.Remove(reference);
        Touch();

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Publication lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Schedules a draft to publish itself at a future instant.</summary>
    public Result SchedulePublication(DateTimeOffset at, DateTimeOffset now)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (Status != GuideStatus.Draft)
        {
            return Result.Failure(GuideErrors.NotDraft);
        }

        if (at <= now)
        {
            return Result.Failure(GuideErrors.ScheduleInPast);
        }

        ScheduledPublishAt = at;
        Touch();

        RaiseDomainEvent(new GuideScheduledDomainEvent
        {
            GuideId = Id,
            ScheduledFor = at
        });

        return Result.Success();
    }

    /// <summary>Calls a scheduled publication off before it fires.</summary>
    public Result CancelScheduledPublication()
    {
        if (ScheduledPublishAt is null)
        {
            return Result.Failure(GuideErrors.NotScheduled);
        }

        ScheduledPublishAt = null;
        Touch();

        RaiseDomainEvent(new GuideScheduleCancelledDomainEvent { GuideId = Id });

        return Result.Success();
    }

    /// <summary>
    /// Publishes the current draft as the next version.
    /// <para>
    /// Guarded by <see cref="IsPublishable"/> because an empty guide in search results is worse
    /// than no guide. Publication is also where the reading time is computed — from the word
    /// count at the platform's fixed reading speed, floored at one minute — so the number is
    /// comparable across guides rather than flattering to any of them.
    /// </para>
    /// </summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(GuideErrors.Deleted);
        }

        if (Status == GuideStatus.Archived)
        {
            return Result.Failure(GuideErrors.ArchivedImmutable);
        }

        if (!IsPublishable)
        {
            return Result.Failure(GuideErrors.NotPublishable);
        }

        var isFirstPublication = FirstPublishedAt is null;
        var readingTime = Math.Max(1, WordCount / GuideLimits.WordsPerMinute);

        Status = GuideStatus.Published;
        PublishedAt = now;
        FirstPublishedAt ??= now;
        ScheduledPublishAt = null;
        ArchivedAt = null;
        CurrentVersion++;
        Reading = Reading.WithReadingTime(readingTime);
        Touch();

        RaiseDomainEvent(new GuidePublishedDomainEvent
        {
            GuideId = Id,
            AuthorId = AuthorId,
            Slug = Slug.Value,
            Title = Title.Value,
            CategoryKey = CategoryKey,
            CountryCode = Location.CountryCode,
            Language = Language.Code,
            Version = CurrentVersion,
            PublishedAt = now,
            IsFirstPublication = isFirstPublication
        });

        RaiseDomainEvent(new GuideVersionCreatedDomainEvent
        {
            GuideId = Id,
            Version = CurrentVersion,
            EditorId = publishedBy ?? AuthorId,
            PublishedAt = now
        });

        return Result.Success();
    }

    /// <summary>Retires a published guide. The versions stay — history is permanent.</summary>
    /// <param name="archivedBy">
    /// Who did it, when a person did. Trust reads it to tell an author retiring their own guide,
    /// which keeps what the guide earned, from anyone else's archive, which takes it back (D-9, Q4).
    /// </param>
    public Result Archive(DateTimeOffset now, UserId? archivedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(GuideErrors.Deleted);
        }

        if (Status != GuideStatus.Published)
        {
            return Result.Failure(GuideErrors.NotPublished);
        }

        Status = GuideStatus.Archived;
        ArchivedAt = now;
        Touch();

        RaiseDomainEvent(new GuideArchivedDomainEvent
        {
            GuideId = Id,
            ArchivedAt = now,
            ArchivedBy = archivedBy
        });

        return Result.Success();
    }

    /// <summary>Returns an archived guide to publication, exactly as it was.</summary>
    public Result RestoreFromArchive(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(GuideErrors.Deleted);
        }

        if (Status != GuideStatus.Archived)
        {
            return Result.Failure(GuideErrors.NotArchived);
        }

        Status = GuideStatus.Published;
        ArchivedAt = null;
        Touch();

        RaiseDomainEvent(new GuideRestoredDomainEvent
        {
            GuideId = Id,
            RestoredAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Featuring
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Staff give the guide featured placement. Published guides only.</summary>
    public Result Feature(DateTimeOffset now, UserId featuredBy)
    {
        ArgumentNullException.ThrowIfNull(featuredBy);

        if (IsDeleted)
        {
            return Result.Failure(GuideErrors.Deleted);
        }

        if (Status != GuideStatus.Published)
        {
            return Result.Failure(GuideErrors.NotPublished);
        }

        if (IsFeatured)
        {
            return Result.Success();
        }

        IsFeatured = true;
        FeaturedAt = now;
        Touch();

        RaiseDomainEvent(new GuideFeaturedDomainEvent
        {
            GuideId = Id,
            FeaturedBy = featuredBy,
            FeaturedAt = now
        });

        return Result.Success();
    }

    /// <summary>Withdraws featured placement. Idempotent.</summary>
    public Result Unfeature()
    {
        if (!IsFeatured)
        {
            return Result.Success();
        }

        IsFeatured = false;
        FeaturedAt = null;
        Touch();

        RaiseDomainEvent(new GuideUnfeaturedDomainEvent { GuideId = Id });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification — projection of Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that a verification claim has gone to Trust. Says nothing about the outcome.
    /// Published guides only — verifying a draft would badge content nobody has seen.
    /// </summary>
    /// <param name="verificationId">
    /// The deciding system's case identifier, when submission returned one.
    /// <para>
    /// <b>Stamping it here is what lets an outcome find this guide again.</b> Trust's events name
    /// a member, and a member may have many guides, so the correlation has to be the case — a
    /// claim submitted without recording its identifier is a claim whose approval arrives with
    /// nowhere to land.
    /// </para>
    /// </param>
    public Result SubmitForVerification(
        DateTimeOffset now, UserId requestedBy, Guid? verificationId = null)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (IsDeleted)
        {
            return Result.Failure(GuideErrors.Deleted);
        }

        if (Status != GuideStatus.Published)
        {
            return Result.Failure(GuideErrors.NotPublished);
        }

        if (TrustMark.State == GuideVerificationState.Pending)
        {
            return Result.Failure(GuideErrors.VerificationAlreadyPending);
        }

        TrustMark = GuideTrustMark.Pending(now, verificationId);
        Touch();

        RaiseDomainEvent(new GuideVerificationRequestedDomainEvent
        {
            GuideId = Id,
            RequestedBy = requestedBy,
            RequestedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies an outcome Trust decided. The only path to a verified mark, reachable only from
    /// a handler for a Trust integration event.
    /// <para>
    /// Guarded on <paramref name="decidedAt"/> because delivery is at-least-once and unordered:
    /// a delayed redelivery of an old approval must not re-verify a guide Trust has since
    /// revoked. A stale decision is dropped silently — success, because from the handler's
    /// point of view the event was processed correctly.
    /// </para>
    /// </summary>
    public Result ApplyVerificationDecision(
        GuideVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset decidedAt)
    {
        if (TrustMark.IsNewerThan(decidedAt))
        {
            return Result.Success();
        }

        var previous = TrustMark.State;

        TrustMark = state switch
        {
            GuideVerificationState.Verified =>
                GuideTrustMark.Verified(verificationId, badge, decidedAt),
            GuideVerificationState.Revoked =>
                GuideTrustMark.Revoked(verificationId, reason, decidedAt),
            GuideVerificationState.Pending =>
                GuideTrustMark.Pending(decidedAt, verificationId),
            _ => GuideTrustMark.Cleared(reason, decidedAt)
        };

        Touch();

        if (previous != TrustMark.State)
        {
            RaiseDomainEvent(new GuideVerificationChangedDomainEvent
            {
                GuideId = Id,
                PreviousState = previous,
                CurrentState = TrustMark.State,
                VerificationId = verificationId,
                Badge = badge,
                Reason = reason,
                ChangedAt = decidedAt
            });
        }

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moderation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Removes the guide from every public surface, reversibly. The guide's own state is
    /// untouched — lifting the decision restores exactly what was there.
    /// </summary>
    public Result Hide(string? reason, UserId moderatorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > GuideLimits.MaxModerationNote)
        {
            return Result.Failure(GuideErrors.ModerationNoteInvalid);
        }

        if (ModerationState == GuideModerationState.Hidden)
        {
            return Result.Success();
        }

        ModerationState = GuideModerationState.Hidden;
        Touch();

        RaiseDomainEvent(new GuideHiddenDomainEvent
        {
            GuideId = Id,
            ModeratorId = moderatorId,
            Reason = trimmed,
            HiddenAt = now
        });

        return Result.Success();
    }

    /// <summary>Lifts a moderation hide. Idempotent.</summary>
    public Result Unhide(UserId? moderatorId = null)
    {
        if (ModerationState != GuideModerationState.Hidden)
        {
            return Result.Success();
        }

        ModerationState = GuideModerationState.Clear;
        Touch();

        RaiseDomainEvent(new GuideUnhiddenDomainEvent
        {
            GuideId = Id,
            ModeratorId = moderatorId
        });

        return Result.Success();
    }

    /// <summary>Puts the guide in the moderation queue without taking it down.</summary>
    public Result Flag(UserId? flaggedBy = null, string? reason = null)
    {
        if (ModerationState != GuideModerationState.Clear)
        {
            return Result.Success();
        }

        ModerationState = GuideModerationState.Flagged;
        Touch();

        RaiseDomainEvent(new GuideFlaggedDomainEvent
        {
            GuideId = Id,
            FlaggedBy = flaggedBy,
            Reason = reason
        });

        return Result.Success();
    }

    /// <summary>Appends to the editorial audit trail. The trail is never edited or pruned.</summary>
    public Result AddModerationNote(Guid noteId, UserId authorId, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        var trimmed = note?.Trim();

        if (noteId == Guid.Empty
            || string.IsNullOrEmpty(trimmed)
            || trimmed.Length > GuideLimits.MaxModerationNote)
        {
            return Result.Failure(GuideErrors.ModerationNoteInvalid);
        }

        _moderationNotes.Add(GuideModerationNote.Create(noteId, authorId, trimmed, now));
        Touch();

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Engagement counters — never a revision bump, never an update event
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A reader viewed the guide. Not an edit — no revision bump, no event.</summary>
    public void RecordView(bool unique = false) => Engagement = Engagement.WithView(unique);

    /// <summary>The vote handler adjusts the rollup after writing the vote row.</summary>
    public void ApplyVoteDelta(int helpfulDelta, int notHelpfulDelta) =>
        Engagement = Engagement.WithVoteDelta(helpfulDelta, notHelpfulDelta);

    /// <summary>Moves the saved count by a delta. The save handlers recount instead, with <see cref="ApplyBookmarkCount"/>.</summary>
    public void ApplyBookmarkDelta(int delta) => Engagement = Engagement.WithBookmarkDelta(delta);

    /// <summary>Community's count of saves, replaced rather than incremented — Community owns it.</summary>
    public void ApplyBookmarkCount(int count) => Engagement = Engagement.WithBookmarkCount(count);

    /// <summary>Community's comment count, replaced rather than incremented — Community owns it.</summary>
    public void ApplyCommentCount(int count) => Engagement = Engagement.WithCommentCount(count);

    /// <summary>A reader shared the guide.</summary>
    public void RecordShare() => Engagement = Engagement.WithShare();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the guide deleted. Sets the fields directly rather than relying on the change
    /// tracker, for the reason B6 records: a delete that happens in the tracker cannot also
    /// raise an event, and downstream de-indexing needs one.
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new GuideDeletedDomainEvent
        {
            GuideId = Id,
            DeletedAt = timestamp
        });
    }

    /// <summary>Undoes a soft delete. The guide resumes in whatever status it held.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        RaiseDomainEvent(new GuideUndeletedDomainEvent { GuideId = Id });
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    private void Touch() => Revision++;

    /// <summary>The shared guard for every edit: not deleted, not archived, revision current.</summary>
    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(GuideErrors.Deleted);
        }

        if (Status == GuideStatus.Archived)
        {
            return Result.Failure(GuideErrors.ArchivedImmutable);
        }

        return EnsureRevision(baseRevision);
    }

    /// <summary>Validates the drafts as a set and rebuilds the owned graph from them.</summary>
    private Result ApplyStructure(IReadOnlyList<GuideSectionDraft> sections)
    {
        if (sections.Count == 0)
        {
            return Result.Failure(GuideErrors.StructureEmpty);
        }

        if (sections.Count > GuideLimits.MaxSections)
        {
            return Result.Failure(GuideErrors.SectionLimit);
        }

        var sectionIds = new HashSet<Guid>();
        var blockIds = new HashSet<Guid>();

        foreach (var draft in sections)
        {
            if (draft is null || !sectionIds.Add(draft.SectionId))
            {
                return Result.Failure(GuideErrors.SectionInvalid);
            }

            foreach (var block in draft.Blocks)
            {
                if (block is null || !blockIds.Add(block.BlockId))
                {
                    return Result.Failure(GuideErrors.BlockInvalid);
                }
            }
        }

        var rebuilt = new List<GuideSection>(sections.Count);

        foreach (var draft in sections.OrderBy(section => section.Order))
        {
            var section = GuideSection.Create(draft);

            if (section.IsFailure)
            {
                return Result.Failure(section.Error);
            }

            rebuilt.Add(section.Value);
        }

        _sections.Clear();
        _sections.AddRange(rebuilt);

        return Result.Success();
    }

    private void RaiseContentUpdated(UserId? changedBy) =>
        RaiseDomainEvent(new GuideContentUpdatedDomainEvent
        {
            GuideId = Id,
            ChangedBy = changedBy,
            SectionCount = _sections.Count,
            BlockCount = _sections.Sum(section => section.Blocks.Count)
        });

    private static string? NormalizeCategoryKey(string? categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return null;
        }

        var key = categoryKey.Trim().ToLowerInvariant();

        var valid = key.Length <= GuideLimits.MaxCategoryKey
            && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !key.StartsWith('-')
            && !key.EndsWith('-')
            && !key.Contains("--", StringComparison.Ordinal);

        return valid ? key : null;
    }
}
