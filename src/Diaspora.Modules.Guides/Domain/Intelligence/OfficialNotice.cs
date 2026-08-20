using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// Something an authority said about a country, and when it stops being true.
/// <para>
/// <b>This is not a guide, and every rule on it follows from that.</b> A guide is somebody's
/// experience, improves by being edited, and is worth more the longer it lives. A notice is a claim
/// about what an institution announced: it is worth nothing unless somebody confirmed the source, it
/// stops being true on a date, and correcting it after publication would rewrite history that
/// members have already acted on. The Guides module owns both because they answer one member's
/// question — "what do I need to know about this country" — and splitting them across two bounded
/// contexts would mean two owners of the answer.
/// </para>
/// <para>
/// <b>Content freezes at publication, and a correction is a supersede.</b> Draft text is editable;
/// editing verified text sends it back to draft, because a verifier confirmed <em>that</em> wording
/// and not the next one; published text cannot be edited at all. What stays editable while published
/// is presentation — where it ranks, what it links to, which translations exist — none of which
/// changes what the authority said. The alternative, an editable published notice, is a document
/// whose meaning can change under a member who read it yesterday, with no record that it did.
/// </para>
/// <para>
/// <b>Archiving is the soft delete, and there is one gone state.</b> B8.1 made the same call for its
/// navigation rows: carrying an archived status <em>and</em> a delete flag gives two ways for a row
/// to be absent, two queries that disagree, and a restore that fixes one of them. Superseded is
/// deliberately <em>not</em> a gone state — a superseded notice stays readable forever, pointing at
/// its replacement, because somebody who followed the old rule needs to see what it said.
/// </para>
/// </summary>
public sealed class OfficialNotice
    : AggregateRoot<OfficialNoticeId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<NoticeLocalization> _localizations = [];
    private readonly List<NoticeAttachment> _attachments = [];
    private readonly List<NoticeReference> _references = [];
    private readonly List<NoticeLanguage> _languages = [];

    private OfficialNotice(
        OfficialNoticeId id,
        string countryCode,
        string categoryKey,
        IssuingAuthorityId authorityId,
        string title,
        string summary,
        string content,
        NoticeSeverity severity,
        DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        CategoryKey = categoryKey;
        AuthorityId = authorityId;
        Title = title;
        Summary = summary;
        Content = content;
        Status = NoticeStatus.Draft;
        SetSeverity(severity);
        VerificationState = NoticeVerificationState.Unverified;
        CreatedAt = createdAt;
    }

    private OfficialNotice() { }

    // ── Subject ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Upper-case ISO 3166-1 alpha-2. A notice is always about exactly one country.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>
    /// A sub-national area, when the notice does not apply nationwide.
    /// <para>
    /// Free text rather than a B4 identifier, and deliberately so. B4 owns the geographic hierarchy
    /// and a notice about "the northern districts" names an area no hierarchy has a node for. When
    /// the platform grows region-scoped intelligence, this becomes the migration's input rather than
    /// its obstacle.
    /// </para>
    /// </summary>
    public string? Region { get; private set; }

    /// <summary>The notice category key. Filed, subscribed to and routed on.</summary>
    public string CategoryKey { get; private set; } = string.Empty;

    /// <summary>Who says so. The field that makes this official rather than merely posted.</summary>
    public IssuingAuthorityId AuthorityId { get; private set; } = null!;

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public string Title { get; private set; } = string.Empty;

    /// <summary>The card text — what a dashboard, a search hit and a notification all render.</summary>
    public string Summary { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    // ── Classification ──────────────────────────────────────────────────────────────────────

    public NoticeStatus Status { get; private set; }

    public NoticeSeverity Severity { get; private set; }

    /// <summary>
    /// The severity as a number, maintained in step with <see cref="Severity"/>.
    /// <para>
    /// <b>Denormalised because the enum is stored as text, and the default sort orders by
    /// severity.</b> Text is the platform's convention for enums and it is the right one — a column
    /// holding <c>Critical</c> is legible to whoever reads the table at three in the morning. But
    /// <c>ORDER BY severity DESC</c> over text sorts alphabetically, which puts
    /// <c>Informational</c> above <c>Critical</c> and makes the briefing order exactly backwards on
    /// the screen where being wrong matters most. The two are set together in one private helper, so
    /// there is no path by which they can drift.
    /// </para>
    /// </summary>
    public int SeverityRank { get; private set; }

    /// <summary>
    /// A curator's thumb on the scale, higher first.
    /// <para>
    /// Separate from severity because they answer different questions. Severity is how bad the news
    /// is; priority is what an editor wants at the top of a country's page this week. A single field
    /// would force an editor wanting to pin a routine passport-office notice to call it critical,
    /// and B13 would then wake people up for it.
    /// </para>
    /// </summary>
    public int Priority { get; private set; }

    // ── Provenance ──────────────────────────────────────────────────────────────────────────

    /// <summary>The authority's own reference — a circular, gazette or file number.</summary>
    public string? ReferenceNumber { get; private set; }

    /// <summary>The authority's own page. Absolute http or https, checked on the way in.</summary>
    public string? SourceUrl { get; private set; }

    // ── Dates ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the <em>authority</em> issued it, which is not when this platform published it.
    /// <para>
    /// Both exist because both are asked. A member wants to know how old the announcement is; an
    /// editor wants to know how long it sat in the queue. One field would answer whichever question
    /// the last person to write it had in mind.
    /// </para>
    /// </summary>
    public DateTimeOffset? PublicationDate { get; private set; }

    /// <summary>When the thing announced starts applying. Frequently after the announcement.</summary>
    public DateTimeOffset? EffectiveFrom { get; private set; }

    /// <summary>When it stops applying. Null means indefinitely, which most rules are.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>When this platform made it visible.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>
    /// When the retirement sweep noticed the expiry had passed.
    /// <para>
    /// <b>A marker, not a second copy of the expiry, and it exists so the sweep is idempotent.</b>
    /// Whether a notice has expired is derived from <see cref="ExpiresAt"/> and needs nothing stored.
    /// Whether anybody has been <em>told</em> is not derivable, and without this the sweep would
    /// re-announce every expired notice on every pass — bumping a revision an editor is holding and
    /// filling the outbox with the same message forever.
    /// </para>
    /// </summary>
    public DateTimeOffset? ExpiredAt { get; private set; }

    /// <summary>
    /// When somebody last checked that it is still true.
    /// <para>
    /// The single most useful field on an ageing knowledge base, and the one nobody adds until they
    /// have one. An indefinite notice with no expiry is not wrong; a two-year-old one nobody has
    /// looked at is a different thing from one confirmed last week, and only this distinguishes them.
    /// </para>
    /// </summary>
    public DateTimeOffset? LastReviewedAt { get; private set; }

    public UserId? LastReviewedBy { get; private set; }

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public NoticeVerificationState VerificationState { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public UserId? VerifiedBy { get; private set; }

    /// <summary>
    /// The Trust case a submitted verification opened.
    /// <para>
    /// <b>Stored, and B8 records why in blood.</b> A guide submitted a verification without keeping
    /// the case identifier the inbound projector correlates on, so every approval Trust returned had
    /// nowhere to land — each half correct alone, and only an end-to-end test could see it. This
    /// field is that defect not repeated.
    /// </para>
    /// </summary>
    public Guid? VerificationCaseId { get; private set; }

    /// <summary>Why a verifier decided what they decided. Shown to editors, never to readers.</summary>
    public string? VerificationNote { get; private set; }

    // ── Supersession ────────────────────────────────────────────────────────────────────────

    /// <summary>The notice that replaced this one. Set once and never cleared.</summary>
    public OfficialNoticeId? SupersededById { get; private set; }

    public DateTimeOffset? SupersededAt { get; private set; }

    // ── Engagement ──────────────────────────────────────────────────────────────────────────

    /// <summary>How many times it was opened.</summary>
    public long ViewCount { get; private set; }

    /// <summary>How many readers followed the official source or a related link.</summary>
    public long ClickThroughCount { get; private set; }

    /// <summary>How many readers reached the end. The signal that the summary was not enough.</summary>
    public long CompletionCount { get; private set; }

    // ── Collections ─────────────────────────────────────────────────────────────────────────

    /// <summary>Human translations. Whole-notice or not at all.</summary>
    public IReadOnlyList<NoticeLocalization> Localizations => _localizations.AsReadOnly();

    /// <summary>Media references — the scanned circular, the new form. Never URLs.</summary>
    public IReadOnlyList<NoticeAttachment> Attachments => _attachments.AsReadOnly();

    /// <summary>What else to look at. Identifiers and kinds, never copies.</summary>
    public IReadOnlyList<NoticeReference> References => _references.AsReadOnly();

    /// <summary>The languages the authority itself published in.</summary>
    public IReadOnlyList<NoticeLanguage> Languages => _languages.AsReadOnly();

    // ── Concurrency and audit ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The optimistic concurrency token, bumped on every editorial change and on no counter.
    /// <para>
    /// An intelligence desk is edited by a small team all at once during exactly the week something
    /// is happening in a country, which is the week a lost update matters most. Counters are excluded
    /// on B8's rule: a view is not an edit, and making one bump the token would give an editor a
    /// conflict because somebody was reading.
    /// </para>
    /// </summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Whether a reader outside the editorial team can see this at all.</summary>
    public bool IsReaderVisible =>
        Status is NoticeStatus.Published or NoticeStatus.Superseded;

    /// <summary>
    /// Whether the notice is live guidance right now rather than history or a future rule.
    /// <para>
    /// The predicate behind the "active only" filter, expressed once. A notice that has expired, been
    /// superseded, or has not taken effect yet is all still readable — it is simply no longer the
    /// answer to "what applies today", and every listing that asked that question separately would
    /// eventually disagree about the edges.
    /// </para>
    /// </summary>
    public bool IsActiveAt(DateTimeOffset moment) =>
        Status == NoticeStatus.Published
        && (EffectiveFrom is null || EffectiveFrom <= moment)
        && (ExpiresAt is null || ExpiresAt > moment);

    /// <summary>Whether the expiry has passed, whatever the status says.</summary>
    public bool HasExpiredAt(DateTimeOffset moment) =>
        ExpiresAt is not null && ExpiresAt <= moment;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Construction
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<OfficialNotice> Create(
        OfficialNoticeId id,
        string? countryCode,
        string? categoryKey,
        IssuingAuthorityId authorityId,
        string? title,
        string? summary,
        string? content,
        NoticeSeverity severity,
        DateTimeOffset now,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(authorityId);

        var country = IntelligenceNormalization.Country(countryCode);

        if (country is null)
        {
            return Result.Failure<OfficialNotice>(IntelligenceErrors.CountryInvalid);
        }

        var category = IntelligenceNormalization.Key(categoryKey);

        if (category is null)
        {
            return Result.Failure<OfficialNotice>(IntelligenceErrors.CategoryKeyInvalid);
        }

        var parsed = ParseContent(title, summary, content);

        if (parsed.IsFailure)
        {
            return Result.Failure<OfficialNotice>(parsed.Error);
        }

        var (trimmedTitle, trimmedSummary, trimmedContent) = parsed.Value;

        var notice = new OfficialNotice(
            id,
            country,
            category,
            authorityId,
            trimmedTitle,
            trimmedSummary,
            trimmedContent,
            Enum.IsDefined(severity) ? severity : NoticeSeverity.Informational,
            now);

        notice.RaiseChanged(IntelligenceChangeKinds.Created, createdBy);

        return Result.Success(notice);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rewrites the text.
    /// <para>
    /// <b>Editing a verified notice sends it back to draft.</b> A verifier confirmed a specific
    /// wording against a specific source; the next wording is a different claim and has not been
    /// checked. Silently keeping the verification would let an editor put anything at all under a
    /// confirmed badge by verifying something innocuous first.
    /// </para>
    /// </summary>
    public Result UpdateContent(string? title, string? summary, string? content, UserId? changedBy)
    {
        if (!IsContentEditable)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        var parsed = ParseContent(title, summary, content);

        if (parsed.IsFailure)
        {
            return parsed;
        }

        (Title, Summary, Content) = parsed.Value;

        InvalidateVerification();

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Moves the notice to a different category, severity or priority.</summary>
    public Result Reclassify(
        string? categoryKey, NoticeSeverity severity, int priority, UserId? changedBy)
    {
        if (!IsContentEditable)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        var category = IntelligenceNormalization.Key(categoryKey);

        if (category is null)
        {
            return Result.Failure(IntelligenceErrors.CategoryKeyInvalid);
        }

        if (!Enum.IsDefined(severity))
        {
            return Result.Failure(IntelligenceErrors.SeverityInvalid);
        }

        CategoryKey = category;
        Priority = priority;
        SetSeverity(severity);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Changes which body issued this.
    /// <para>
    /// Editable, because transcription errors happen and the wrong ministry on a circular is exactly
    /// the sort of thing a verifier catches. It invalidates the verification for the same reason a
    /// content edit does: who said it is the substance of the claim, not metadata about it.
    /// </para>
    /// </summary>
    public Result Reassign(IssuingAuthorityId authorityId, UserId? changedBy)
    {
        ArgumentNullException.ThrowIfNull(authorityId);

        if (!IsContentEditable)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        AuthorityId = authorityId;

        InvalidateVerification();

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Sets the dates the notice is true between.
    /// <para>
    /// <b>A publication date in the future is refused.</b> It records when the authority issued this,
    /// and an authority cannot have issued something tomorrow. Scheduling a notice for future release
    /// is a different feature with a different field, and quietly overloading this one would make
    /// every "how old is this announcement" answer wrong.
    /// </para>
    /// </summary>
    public Result Schedule(
        DateTimeOffset? publicationDate,
        DateTimeOffset? effectiveFrom,
        DateTimeOffset? expiresAt,
        DateTimeOffset now,
        UserId? changedBy)
    {
        if (!IsContentEditable)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        if (publicationDate is not null && publicationDate > now)
        {
            return Result.Failure(IntelligenceErrors.PublicationInFuture);
        }

        if (effectiveFrom is not null && expiresAt is not null && expiresAt <= effectiveFrom)
        {
            return Result.Failure(IntelligenceErrors.EffectiveAfterExpiry);
        }

        PublicationDate = publicationDate;
        EffectiveFrom = effectiveFrom;
        ExpiresAt = expiresAt;

        // Reachable only through withdraw → restore → reschedule, because dates freeze at
        // publication and a restored notice is a draft again. Worth doing anyway: leaving the marker
        // set would make the second expiry silent, and the sweep would say nothing about a notice it
        // had already announced under a deadline that no longer applies.
        if (ExpiredAt is not null && !HasExpiredAt(now))
        {
            ExpiredAt = null;
        }

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Records where the notice came from.
    /// <para>
    /// The URL is normalized and checked for shape here; whether the <em>host</em> is one this
    /// deployment trusts is a policy question answered above the aggregate, because the answer
    /// differs per deployment and the domain has no business holding a domain allow-list.
    /// </para>
    /// </summary>
    public Result AttributeSource(string? referenceNumber, string? sourceUrl, UserId? changedBy)
    {
        if (!IsContentEditable)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        string? url = null;

        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            url = IntelligenceNormalization.Url(sourceUrl);

            if (url is null)
            {
                return Result.Failure(IntelligenceErrors.SourceUrlInvalid);
            }
        }

        ReferenceNumber = IntelligenceNormalization.Reference(referenceNumber);
        SourceUrl = url;

        InvalidateVerification();

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Names the sub-national area a notice applies to, or clears it.</summary>
    public Result SetRegion(string? region, UserId? changedBy)
    {
        if (!IsContentEditable)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        Region = IntelligenceNormalization.Text(region, IntelligenceLimits.MaxRegion);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Collections
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the declared languages.
    /// <para>
    /// Editable while published, unlike the text. Recording that the authority also issued a French
    /// version does not change what the notice says — it is a fact about the source somebody found
    /// later, and refusing it would leave the record permanently less complete than the truth.
    /// </para>
    /// </summary>
    public Result SetLanguages(IEnumerable<string>? codes, UserId? changedBy)
    {
        if (IsFrozen)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        var requested = codes?.ToList() ?? [];

        if (requested.Count > IntelligenceLimits.MaxNoticeLanguages)
        {
            return Result.Failure(IntelligenceErrors.LanguageLimit);
        }

        var resolved = new List<NoticeLanguage>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var code in requested)
        {
            var language = NoticeLanguage.TryCreate(code);

            if (language is null)
            {
                return Result.Failure(IntelligenceErrors.LocaleInvalid);
            }

            if (seen.Add(language.Code))
            {
                resolved.Add(language);
            }
        }

        _languages.Clear();
        _languages.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the translation set.
    /// <para>
    /// Editable while published, and this is the case that most justifies the rule. A community
    /// translator finishing a Kreol version of a live health advisory three days after it went out is
    /// the system working; refusing it because the notice is published would mean the people who most
    /// needed the translation never get it.
    /// </para>
    /// <para>
    /// A locale that appears twice keeps its first entry — the ordinary cause is a pasted spreadsheet
    /// with a repeated row. An unusable locale tag <em>does</em> fail, because that one is a typo
    /// somebody can fix and would otherwise silently lose a language.
    /// </para>
    /// </summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? Title, string? Summary, string? Content)>? localizations,
        UserId? changedBy)
    {
        if (IsFrozen)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        var requested = localizations?.ToList() ?? [];

        if (requested.Count > IntelligenceLimits.MaxNoticeLocalizations)
        {
            return Result.Failure(IntelligenceErrors.LocalizationLimit);
        }

        var resolved = new List<NoticeLocalization>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, title, summary, content) in requested)
        {
            var localization = NoticeLocalization.TryCreate(locale, title, summary, content);

            if (localization is null)
            {
                return Result.Failure(IntelligenceErrors.LocaleInvalid);
            }

            if (seen.Add(localization.Locale))
            {
                resolved.Add(localization);
            }
        }

        _localizations.Clear();
        _localizations.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the media references. Editable while published, for the same reason.</summary>
    public Result SetAttachments(
        IEnumerable<(Guid MediaId, string? Caption)>? attachments, UserId? changedBy)
    {
        if (IsFrozen)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        var requested = attachments?.ToList() ?? [];

        if (requested.Count > IntelligenceLimits.MaxNoticeAttachments)
        {
            return Result.Failure(IntelligenceErrors.AttachmentLimit);
        }

        var resolved = new List<NoticeAttachment>(requested.Count);
        var seen = new HashSet<Guid>();

        foreach (var (mediaId, caption) in requested)
        {
            if (!seen.Add(mediaId))
            {
                continue;
            }

            var attachment = NoticeAttachment.TryCreate(mediaId, caption, resolved.Count);

            if (attachment is null)
            {
                return Result.Failure(IntelligenceErrors.AttachmentInvalid);
            }

            resolved.Add(attachment);
        }

        _attachments.Clear();
        _attachments.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the related items.
    /// <para>
    /// Editable while published, and it is the collection that changes most: the guide explaining a
    /// new visa rule is usually written <em>after</em> the rule is announced, and linking it is how
    /// the notice stops being a dead end.
    /// </para>
    /// <para>
    /// A repeated (kind, target) pair is refused rather than collapsed. Two links to the same guide
    /// with different labels is a mistake with a visible symptom — the same card twice — and telling
    /// the editor is cheaper than letting them wonder which label won.
    /// </para>
    /// </summary>
    public Result SetReferences(
        IEnumerable<(NoticeReferenceKind Kind, Guid TargetId, string? Label)>? references,
        UserId? changedBy)
    {
        if (IsFrozen)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotEditable);
        }

        var requested = references?.ToList() ?? [];

        if (requested.Count > IntelligenceLimits.MaxNoticeReferences)
        {
            return Result.Failure(IntelligenceErrors.ReferenceLimit);
        }

        var resolved = new List<NoticeReference>(requested.Count);
        var seen = new HashSet<(NoticeReferenceKind, Guid)>();

        foreach (var (kind, targetId, label) in requested)
        {
            if (!seen.Add((kind, targetId)))
            {
                return Result.Failure(IntelligenceErrors.DuplicateReference);
            }

            var reference = NoticeReference.TryCreate(kind, targetId, label, resolved.Count);

            if (reference is null)
            {
                return Result.Failure(IntelligenceErrors.ReferenceInvalid);
            }

            resolved.Add(reference);
        }

        _references.Clear();
        _references.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// The text a reader in one locale should see, falling back through language to the default.
    /// <para>
    /// The same exact→language→default ladder B8.1 uses for navigation labels, and for the same
    /// reason: a reader asking for <c>fr-MU</c> gets the <c>fr</c> translation when nobody has
    /// written a Mauritian French one, which is right far more often than showing them English.
    /// </para>
    /// </summary>
    public (string Title, string Summary, string Content) Localized(string? locale)
    {
        var normalized = IntelligenceNormalization.Locale(locale);

        if (normalized is null)
        {
            return (Title, Summary, Content);
        }

        var exact = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, normalized, StringComparison.Ordinal));

        if (exact is not null)
        {
            return (exact.Title, exact.Summary, exact.Content);
        }

        var language = normalized.Split('-')[0];

        var byLanguage = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, language, StringComparison.Ordinal));

        return byLanguage is null
            ? (Title, Summary, Content)
            : (byLanguage.Title, byLanguage.Summary, byLanguage.Content);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sends a completed draft for verification.
    /// <para>
    /// The completeness check lives here rather than in a validator because it is a rule about the
    /// aggregate's state at a moment, not about a request's shape: a notice can be saved as an empty
    /// draft all day, and the moment it stops being allowed to be incomplete is this one.
    /// </para>
    /// </summary>
    public Result Submit(UserId? changedBy)
    {
        if (Status != NoticeStatus.Draft)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotSubmittable);
        }

        if (string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(Content)
            || string.IsNullOrWhiteSpace(CategoryKey))
        {
            return Result.Failure(IntelligenceErrors.NoticeNotSubmittable);
        }

        Status = NoticeStatus.Submitted;
        VerificationState = NoticeVerificationState.Pending;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Submitted, changedBy);

        return Result.Success();
    }

    /// <summary>Records the Trust case a submission opened, so an approval has somewhere to land.</summary>
    public void RecordVerificationCase(Guid caseId)
    {
        if (caseId != Guid.Empty)
        {
            VerificationCaseId = caseId;
        }
    }

    /// <summary>A verifier confirmed the source. The notice becomes publishable.</summary>
    public Result MarkVerified(DateTimeOffset now, UserId? verifiedBy, string? note = null)
    {
        if (Status != NoticeStatus.Submitted)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotVerifiable);
        }

        Status = NoticeStatus.Verified;
        VerificationState = NoticeVerificationState.Verified;
        VerifiedAt = now;
        VerifiedBy = verifiedBy;
        VerificationNote = IntelligenceNormalization.Text(note, IntelligenceLimits.MaxNote);
        LastReviewedAt = now;
        LastReviewedBy = verifiedBy;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Verified, verifiedBy);

        return Result.Success();
    }

    /// <summary>
    /// A verifier did not confirm the source. The notice returns to draft so it can be fixed.
    /// <para>
    /// Back to draft rather than into a terminal rejected state, because the ordinary cause is a
    /// missing reference number rather than a fabrication, and a workflow that made the editor start
    /// again would lose the body they had already transcribed.
    /// </para>
    /// </summary>
    public Result MarkRejected(DateTimeOffset now, UserId? rejectedBy, string? note = null)
    {
        if (Status != NoticeStatus.Submitted)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotVerifiable);
        }

        Status = NoticeStatus.Draft;
        VerificationState = NoticeVerificationState.Rejected;
        VerifiedAt = null;
        VerifiedBy = null;
        VerificationNote = IntelligenceNormalization.Text(note, IntelligenceLimits.MaxNote);
        LastReviewedAt = now;
        LastReviewedBy = rejectedBy;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Rejected, rejectedBy);

        return Result.Success();
    }

    /// <summary>Makes a verified notice visible.</summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy)
    {
        if (Status == NoticeStatus.Published)
        {
            return Result.Failure(IntelligenceErrors.NoticeAlreadyPublished);
        }

        if (Status != NoticeStatus.Verified)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotPublishable);
        }

        Status = NoticeStatus.Published;
        PublishedAt = now;
        PublicationDate ??= now;
        LastReviewedAt ??= now;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Published, publishedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces this notice with a later one.
    /// <para>
    /// <b>The replacement is named, and that is the point.</b> A superseded notice with no pointer is
    /// a dead end for everybody who was told about the original — the message they received still
    /// links here, and "this is no longer current" without "here is what is" is the worst possible
    /// answer to give somebody who acted on it.
    /// </para>
    /// </summary>
    public Result Supersede(OfficialNoticeId replacement, DateTimeOffset now, UserId? changedBy)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (replacement == Id)
        {
            return Result.Failure(IntelligenceErrors.SupersedeSelf);
        }

        if (Status == NoticeStatus.Superseded)
        {
            return Result.Failure(IntelligenceErrors.NoticeAlreadySuperseded);
        }

        if (Status != NoticeStatus.Published)
        {
            return Result.Failure(IntelligenceErrors.SupersedeNotPublished);
        }

        Status = NoticeStatus.Superseded;
        SupersededById = replacement;
        SupersededAt = now;

        Bump();
        RaiseSuperseded(replacement, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Confirms the notice is still true, without changing a word of it.
    /// <para>
    /// A counter-free, content-free operation that exists solely to move
    /// <see cref="LastReviewedAt"/>. It bumps the revision, because "somebody checked this on
    /// Tuesday" is an editorial fact a second editor should not silently overwrite.
    /// </para>
    /// </summary>
    public Result MarkReviewed(DateTimeOffset now, UserId? reviewedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.NoticeArchived);
        }

        LastReviewedAt = now;
        LastReviewedBy = reviewedBy;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, reviewedBy);

        return Result.Success();
    }

    /// <summary>
    /// Retires a notice whose expiry has passed.
    /// <para>
    /// Called by the sweep, never by a handler. <b>The status deliberately does not move.</b> An
    /// expired notice stays published and stays readable, because "this rule applied until March" is
    /// an answer somebody needs; what changes is that <see cref="IsActiveAt"/> stops returning true,
    /// which is what every "currently applies" listing already filters on. Archiving it instead would
    /// hide the answer, and superseding it would claim a replacement exists when none does.
    /// </para>
    /// <para>
    /// Returns false when there is nothing to do, which is how the sweep stays idempotent and how a
    /// second pass over the same page costs one comparison rather than a second announcement.
    /// </para>
    /// </summary>
    public bool Expire(DateTimeOffset now)
    {
        if (Status != NoticeStatus.Published || ExpiredAt is not null || !HasExpiredAt(now))
        {
            return false;
        }

        ExpiredAt = now;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Expired, null);

        return true;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Engagement
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Counters, and none of them bumps the revision or raises an event.
    /// <para>
    /// B8's rule verbatim: engagement is not an edit. A notice being read by ten thousand people
    /// during a border closure must not produce ten thousand concurrency conflicts for the editor
    /// trying to update it, nor ten thousand outbox messages.
    /// </para>
    /// </summary>
    public void RecordView() => ViewCount++;

    /// <inheritdoc cref="RecordView" />
    public void RecordClickThrough() => ClickThroughCount++;

    /// <inheritdoc cref="RecordView" />
    public void RecordCompletion() => CompletionCount++;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency, soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rejects a write whose caller was looking at an older revision.
    /// <para>
    /// The aggregate-level half of optimistic concurrency; the token on the column is the other. This
    /// catches an editor who sends back a stale revision and can be told precisely what happened; the
    /// token catches the one who sends none.
    /// </para>
    /// </summary>
    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(IntelligenceErrors.RevisionConflict);

    /// <summary>
    /// Archives the notice. Called by the persistence pipeline from a repository removal, never by a
    /// handler — the rule B1.0.1 records for every soft delete on this platform.
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Status = NoticeStatus.Archived;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Archived, Actor(userId));
    }

    /// <summary>
    /// Returns an archived notice to the desk.
    /// <para>
    /// <b>It comes back as a draft, never as published.</b> Restoring is an editorial decision to
    /// look at something again; republishing it unread would put a notice back in front of members
    /// without anybody confirming it is still true, which for a withdrawn official statement is the
    /// one outcome nobody wants.
    /// </para>
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = NoticeStatus.Draft;
        PublishedAt = null;
        ExpiredAt = null;
        VerificationState = NoticeVerificationState.Unverified;
        VerifiedAt = null;
        VerifiedBy = null;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Restored, null);
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

    /// <summary>Whether the substance of the claim may still be rewritten.</summary>
    private bool IsContentEditable =>
        !IsDeleted && Status is NoticeStatus.Draft or NoticeStatus.Verified;

    /// <summary>Whether even presentation is closed. Superseded and archived notices are history.</summary>
    private bool IsFrozen =>
        IsDeleted || Status is NoticeStatus.Superseded or NoticeStatus.Submitted;

    /// <summary>
    /// Drops a verification because the thing verified has changed.
    /// <para>
    /// It returns a <see cref="NoticeStatus.Verified"/> notice to draft rather than leaving it
    /// verified-but-changed, which would be a state whose name is a lie.
    /// </para>
    /// </summary>
    private void InvalidateVerification()
    {
        if (Status != NoticeStatus.Verified)
        {
            return;
        }

        Status = NoticeStatus.Draft;
        VerificationState = NoticeVerificationState.Unverified;
        VerifiedAt = null;
        VerifiedBy = null;
    }

    private static Result<(string Title, string Summary, string Content)> ParseContent(
        string? title, string? summary, string? content)
    {
        var trimmedTitle = IntelligenceNormalization.Text(title, IntelligenceLimits.MaxNoticeTitle);

        if (trimmedTitle is null)
        {
            return Result.Failure<(string, string, string)>(IntelligenceErrors.TitleInvalid);
        }

        var trimmedSummary = IntelligenceNormalization.Text(
            summary, IntelligenceLimits.MaxNoticeSummary);

        if (trimmedSummary is null)
        {
            return Result.Failure<(string, string, string)>(IntelligenceErrors.SummaryInvalid);
        }

        var trimmedContent = IntelligenceNormalization.Text(
            content, IntelligenceLimits.MaxNoticeContent);

        return trimmedContent is null
            ? Result.Failure<(string, string, string)>(IntelligenceErrors.ContentInvalid)
            : Result.Success((trimmedTitle, trimmedSummary, trimmedContent));
    }

    /// <summary>
    /// The one place severity is written, so the text and the rank cannot drift.
    /// <para>
    /// Two assignments in one method rather than two properties a caller sets: the alternative is a
    /// defect that surfaces as a country page whose ordering is subtly wrong, months later, with both
    /// columns individually looking correct.
    /// </para>
    /// </summary>
    private void SetSeverity(NoticeSeverity severity)
    {
        Severity = severity;
        SeverityRank = (int)severity;
    }

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(BuildEvent(change, changedBy, supersededBy: null));

    private void RaiseSuperseded(OfficialNoticeId replacement, UserId? changedBy) =>
        RaiseDomainEvent(
            BuildEvent(IntelligenceChangeKinds.Superseded, changedBy, replacement));

    private OfficialNoticeChangedDomainEvent BuildEvent(
        string change, UserId? changedBy, OfficialNoticeId? supersededBy) =>
        new()
        {
            OfficialNoticeId = Id,
            CountryCode = CountryCode,
            CategoryKey = CategoryKey,
            Title = Title,
            Summary = Summary,
            Change = change,
            Status = Status,
            Severity = Severity,
            AuthorityId = AuthorityId,
            PublicationDate = PublicationDate,
            EffectiveFrom = EffectiveFrom,
            ExpiresAt = ExpiresAt,
            SupersededBy = supersededBy,
            ChangedBy = changedBy
        };

    /// <summary>
    /// The audit interceptor hands the actor down as a string, because that is what it stamps into
    /// the row. The event wants an identifier. A caller that is not a member — a sweep, an import —
    /// leaves it null rather than inventing one.
    /// </summary>
    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
