using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Visas;

/// <summary>
/// What the rules are for getting into a country, and who they apply to.
/// <para>
/// <b>The only content in this phase that is a relation between two countries.</b> A notice is about
/// a country; an advisory is about a country <em>for</em> a nationality, and the pair is what makes
/// it useful — "visa-free for 90 days" is true and useless without knowing whose passport. A null
/// <see cref="AppliesToCountryCode"/> means the rules apply to everybody, which is the honest way to
/// express a general rule rather than writing one advisory per nationality on earth.
/// </para>
/// <para>
/// <b>It describes; it never processes.</b> No applicant, no case, no per-person status, no fee
/// collection. The brief puts application processing out of scope and the aggregate is where that is
/// enforced rather than promised: there is nowhere for an application to live.
/// </para>
/// <para>
/// <b>A rule change is a notice; the advisory is then edited.</b> The two work together and neither
/// replaces the other — an <c>OfficialNotice</c> is the announcement a member is told about and
/// which stays in the record forever, and this is the standing description that is always current.
/// Trying to make one do both would give either an announcement that silently rewrites itself or a
/// description that grows a new copy every time a fee changes.
/// </para>
/// </summary>
public sealed class VisaAdvisory : AggregateRoot<VisaAdvisoryId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<VisaLine> _lines = [];
    private readonly List<VisaGuideReference> _relatedGuides = [];
    private readonly List<VisaAdvisoryLocalization> _localizations = [];

    private VisaAdvisory(
        VisaAdvisoryId id,
        string countryCode,
        string? appliesToCountryCode,
        IssuingAuthorityId authorityId,
        string title,
        string summary,
        DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        AppliesToCountryCode = appliesToCountryCode;
        AuthorityId = authorityId;
        Title = title;
        Summary = summary;
        Status = VisaAdvisoryStatus.Draft;
        CreatedAt = createdAt;
    }

    private VisaAdvisory() { }

    // ── Subject ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the destination the rules govern entry to.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>
    /// The nationality the rules apply to, or null for everybody. Upper-case ISO 3166-1 alpha-2.
    /// </summary>
    public string? AppliesToCountryCode { get; private set; }

    /// <summary>Who says so. The field that makes this authoritative rather than a summary.</summary>
    public IssuingAuthorityId AuthorityId { get; private set; } = null!;

    /// <summary>
    /// The visa class — <c>Student</c>, <c>Skilled Worker</c>, <c>Visitor</c>.
    /// <para>
    /// Free text, because every country names its own classes and an enum would need a member per
    /// country per class. Clients group on it; nothing branches on it.
    /// </para>
    /// </summary>
    public string? VisaCategory { get; private set; }

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string? Content { get; private set; }

    /// <summary>How long it takes, as the authority states it — "10 to 15 working days".</summary>
    public string? ProcessingTime { get; private set; }

    /// <summary>Where somebody applies. Absolute http or https, checked on the way in.</summary>
    public string? ApplicationUrl { get; private set; }

    /// <summary>The authority's own reference for the rule.</summary>
    public string? OfficialReference { get; private set; }

    /// <summary>The authority's own page describing it.</summary>
    public string? SourceUrl { get; private set; }

    // ── Lifecycle and dates ─────────────────────────────────────────────────────────────────

    public VisaAdvisoryStatus Status { get; private set; }

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <inheritdoc cref="Intelligence.OfficialNotice.LastReviewedAt" />
    public DateTimeOffset? LastReviewedAt { get; private set; }

    public UserId? LastReviewedBy { get; private set; }

    // ── Collections ─────────────────────────────────────────────────────────────────────────

    /// <summary>Requirements and required documents, in one collection with a discriminator.</summary>
    public IReadOnlyList<VisaLine> Lines => _lines.AsReadOnly();

    /// <summary>Guides that explain how to actually do it. Identifiers, never copies.</summary>
    public IReadOnlyList<VisaGuideReference> RelatedGuides => _relatedGuides.AsReadOnly();

    /// <summary>Translated framing over the authoritative list.</summary>
    public IReadOnlyList<VisaAdvisoryLocalization> Localizations => _localizations.AsReadOnly();

    /// <summary>The conditions, in order.</summary>
    public IReadOnlyList<VisaLine> Requirements =>
        [.. _lines.Where(line => line.Kind == VisaLineKind.Requirement).OrderBy(line => line.Position)];

    /// <summary>The papers to bring, in order.</summary>
    public IReadOnlyList<VisaLine> Documents =>
        [.. _lines.Where(line => line.Kind == VisaLineKind.Document).OrderBy(line => line.Position)];

    // ── Concurrency and audit ───────────────────────────────────────────────────────────────

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Whether these are the rules that apply right now rather than history or a future set.</summary>
    public bool IsActiveAt(DateTimeOffset moment) =>
        Status == VisaAdvisoryStatus.Published
        && (EffectiveFrom is null || EffectiveFrom <= moment)
        && (ExpiresAt is null || ExpiresAt > moment);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Construction and editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<VisaAdvisory> Create(
        VisaAdvisoryId id,
        string? countryCode,
        string? appliesToCountryCode,
        IssuingAuthorityId authorityId,
        string? title,
        string? summary,
        DateTimeOffset now,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(authorityId);

        var country = IntelligenceNormalization.Country(countryCode);

        if (country is null)
        {
            return Result.Failure<VisaAdvisory>(IntelligenceErrors.CountryInvalid);
        }

        // Absent means "everybody" and is not an error; supplied and unusable is.
        string? appliesTo = null;

        if (!string.IsNullOrWhiteSpace(appliesToCountryCode))
        {
            appliesTo = IntelligenceNormalization.Country(appliesToCountryCode);

            if (appliesTo is null)
            {
                return Result.Failure<VisaAdvisory>(IntelligenceErrors.CountryInvalid);
            }
        }

        var trimmedTitle = IntelligenceNormalization.Text(title, IntelligenceLimits.MaxVisaTitle);

        if (trimmedTitle is null)
        {
            return Result.Failure<VisaAdvisory>(IntelligenceErrors.VisaTitleInvalid);
        }

        var trimmedSummary = IntelligenceNormalization.Text(
            summary, IntelligenceLimits.MaxVisaSummary);

        if (trimmedSummary is null)
        {
            return Result.Failure<VisaAdvisory>(IntelligenceErrors.SummaryInvalid);
        }

        var advisory = new VisaAdvisory(
            id, country, appliesTo, authorityId, trimmedTitle, trimmedSummary, now);

        advisory.RaiseChanged(IntelligenceChangeKinds.Created, createdBy);

        return Result.Success(advisory);
    }

    /// <summary>
    /// Rewrites the advisory.
    /// <para>
    /// <b>Editable while published, unlike a notice, and the asymmetry is the point.</b> A notice is
    /// a statement made at a moment and freezing it is what keeps the record honest. An advisory is a
    /// standing description of what is true <em>now</em>, and freezing it would make it wrong the day
    /// a fee changed. What preserves the history is that a rule change publishes a notice, and the
    /// notice is permanent.
    /// </para>
    /// </summary>
    public Result Update(
        string? title,
        string? summary,
        string? content,
        string? visaCategory,
        string? processingTime,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        var trimmedTitle = IntelligenceNormalization.Text(title, IntelligenceLimits.MaxVisaTitle);

        if (trimmedTitle is null)
        {
            return Result.Failure(IntelligenceErrors.VisaTitleInvalid);
        }

        var trimmedSummary = IntelligenceNormalization.Text(
            summary, IntelligenceLimits.MaxVisaSummary);

        if (trimmedSummary is null)
        {
            return Result.Failure(IntelligenceErrors.SummaryInvalid);
        }

        Title = trimmedTitle;
        Summary = trimmedSummary;
        Content = IntelligenceNormalization.Text(content, IntelligenceLimits.MaxVisaContent);
        VisaCategory = IntelligenceNormalization.Text(
            visaCategory, IntelligenceLimits.MaxVisaCategory);
        ProcessingTime = IntelligenceNormalization.Text(
            processingTime, IntelligenceLimits.MaxProcessingTime);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Records where the rules come from and where somebody applies.</summary>
    public Result AttributeSource(
        string? officialReference, string? sourceUrl, string? applicationUrl, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        var source = NormalizeOptionalUrl(sourceUrl, out var sourceValid);
        var application = NormalizeOptionalUrl(applicationUrl, out var applicationValid);

        if (!sourceValid || !applicationValid)
        {
            return Result.Failure(IntelligenceErrors.SourceUrlInvalid);
        }

        OfficialReference = IntelligenceNormalization.Reference(officialReference);
        SourceUrl = source;
        ApplicationUrl = application;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Sets the window the rules are true in.</summary>
    public Result Schedule(
        DateTimeOffset? effectiveFrom, DateTimeOffset? expiresAt, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        if (effectiveFrom is not null && expiresAt is not null && expiresAt <= effectiveFrom)
        {
            return Result.Failure(IntelligenceErrors.EffectiveAfterExpiry);
        }

        EffectiveFrom = effectiveFrom;
        ExpiresAt = expiresAt;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Moves the advisory to a different issuing authority.</summary>
    public Result Reassign(IssuingAuthorityId authorityId, UserId? changedBy)
    {
        ArgumentNullException.ThrowIfNull(authorityId);

        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        AuthorityId = authorityId;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Collections
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces both lists at once.
    /// <para>
    /// Both together rather than one at a time, because a caller moving a line between them would
    /// otherwise have to make two calls and would be momentarily holding an advisory that lists the
    /// same sentence twice. Positions are renumbered from zero within each list, which is also what
    /// makes the composite key stable.
    /// </para>
    /// </summary>
    public Result SetLines(
        IEnumerable<string>? requirements, IEnumerable<string>? documents, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        var requestedRequirements = requirements?.ToList() ?? [];
        var requestedDocuments = documents?.ToList() ?? [];

        if (requestedRequirements.Count > IntelligenceLimits.MaxVisaRequirements)
        {
            return Result.Failure(IntelligenceErrors.VisaRequirementLimit);
        }

        if (requestedDocuments.Count > IntelligenceLimits.MaxVisaDocuments)
        {
            return Result.Failure(IntelligenceErrors.VisaDocumentLimit);
        }

        var resolved = new List<VisaLine>(requestedRequirements.Count + requestedDocuments.Count);

        Collect(VisaLineKind.Requirement, requestedRequirements, resolved);
        Collect(VisaLineKind.Document, requestedDocuments, resolved);

        _lines.Clear();
        _lines.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the guides that explain how to do this.</summary>
    public Result SetRelatedGuides(IEnumerable<Guid>? guideIds, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        var requested = guideIds?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxRecommendedReads)
        {
            return Result.Failure(IntelligenceErrors.ReferenceLimit);
        }

        var resolved = new List<VisaGuideReference>(requested.Count);
        var seen = new HashSet<Guid>();

        foreach (var guideId in requested)
        {
            if (!seen.Add(guideId))
            {
                continue;
            }

            var reference = VisaGuideReference.TryCreate(guideId, resolved.Count);

            if (reference is null)
            {
                return Result.Failure(IntelligenceErrors.ReferenceInvalid);
            }

            resolved.Add(reference);
        }

        _relatedGuides.Clear();
        _relatedGuides.AddRange(resolved);

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the translation set.</summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? Title, string? Summary, string? Content)>? localizations,
        UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        var requested = localizations?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxLocalizations)
        {
            return Result.Failure(IntelligenceErrors.LocalizationLimit);
        }

        var resolved = new List<VisaAdvisoryLocalization>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, title, summary, content) in requested)
        {
            var localization = VisaAdvisoryLocalization.TryCreate(locale, title, summary, content);

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

    /// <summary>The framing for a locale, with the usual exact→language→default fallback.</summary>
    public (string Title, string Summary, string? Content) Localized(string? locale)
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

    /// <summary>Makes the advisory visible.</summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        if (Status == VisaAdvisoryStatus.Published)
        {
            return Result.Failure(IntelligenceErrors.NoticeAlreadyPublished);
        }

        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Summary))
        {
            return Result.Failure(IntelligenceErrors.VisaNotPublishable);
        }

        Status = VisaAdvisoryStatus.Published;
        PublishedAt = now;
        LastReviewedAt ??= now;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Published, publishedBy);

        return Result.Success();
    }

    /// <inheritdoc cref="Intelligence.OfficialNotice.MarkReviewed" />
    public Result MarkReviewed(DateTimeOffset now, UserId? reviewedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(IntelligenceErrors.VisaArchived);
        }

        LastReviewedAt = now;
        LastReviewedBy = reviewedBy;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, reviewedBy);

        return Result.Success();
    }

    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(IntelligenceErrors.RevisionConflict);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <inheritdoc cref="Intelligence.NoticeCategory.MarkDeleted" />
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Status = VisaAdvisoryStatus.Archived;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Archived, Actor(userId));
    }

    /// <summary>
    /// Returns an archived advisory to the desk as a draft.
    /// <para>
    /// A draft rather than republished, on the same reasoning a notice's restore uses: bringing back
    /// a withdrawn description of immigration rules without anybody confirming it is still true is
    /// the one outcome nobody wants.
    /// </para>
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = VisaAdvisoryStatus.Draft;
        PublishedAt = null;

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

    /// <summary>
    /// Turns a list of sentences into positioned lines of one kind.
    /// <para>
    /// A line that is only whitespace is skipped rather than refused. Pasting a list out of a
    /// government PDF reliably brings blank lines with it, and rejecting a forty-line transcription
    /// over one of them teaches the editor nothing they can act on. Positions are assigned after the
    /// skip, so the stored list is contiguous and the composite key stays stable.
    /// </para>
    /// </summary>
    private static void Collect(
        VisaLineKind kind, IReadOnlyList<string> texts, List<VisaLine> into)
    {
        var position = 0;

        foreach (var text in texts)
        {
            var line = VisaLine.TryCreate(kind, position, text);

            if (line is null)
            {
                continue;
            }

            into.Add(line);
            position++;
        }
    }

    /// <summary>
    /// Normalizes an optional URL, distinguishing "not supplied" from "supplied and unusable".
    /// <para>
    /// Both produce a null string and only one of them is an error — the distinction a single
    /// nullable return cannot carry, and the reason this takes an out parameter.
    /// </para>
    /// </summary>
    private static string? NormalizeOptionalUrl(string? value, out bool valid)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            valid = true;
            return null;
        }

        var normalized = IntelligenceNormalization.Url(value);

        valid = normalized is not null;

        return normalized;
    }

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new VisaAdvisoryChangedDomainEvent
        {
            VisaAdvisoryId = Id,
            CountryCode = CountryCode,
            AppliesToCountryCode = AppliesToCountryCode,
            Title = Title,
            VisaCategory = VisaCategory,
            Change = change,
            Status = Status,
            AuthorityId = AuthorityId,
            EffectiveFrom = EffectiveFrom,
            ExpiresAt = ExpiresAt,
            ChangedBy = changedBy
        });

    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
