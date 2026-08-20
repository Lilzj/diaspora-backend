using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Directories;

/// <summary>
/// One country's knowledge directory — the answer to "I am moving to Mauritius, what should I
/// learn first".
/// <para>
/// <b>The aggregate that makes the platform country-first.</b> Everything else on Diaspora is
/// organised by the kind of thing it is: guides by category, jobs by employer, homes by
/// neighbourhood. A member does not experience their life that way. They have moved somewhere, and
/// everything they need to know is a property of <em>where</em> before it is a property of what.
/// This aggregate is that inversion, made durable.
/// </para>
/// <para>
/// <b>It owns arrangement and owns no content.</b> The entries name catalogue keys; the featured
/// rows name guide identifiers. Not one field here holds a title, a body, a word count or an
/// author — because the moment it did, publishing a guide would have to remember to update a
/// second copy of it, and one day it would not. Every read resolves identifiers through B8's own
/// contracts at the point of rendering.
/// </para>
/// <para>
/// <b>Publication is separate from arrangement, and both are separate from archival.</b> A
/// directory is arranged over weeks before anyone sees it, published when it is coherent,
/// unpublished when a country's rules change out from under it, and archived when the platform
/// stops serving it. Collapsing any two of those loses a state a curator actually needs: an
/// unpublished directory is still on their desk, and an archived one is not.
/// </para>
/// </summary>
public sealed class CountryKnowledgeDirectory
    : AggregateRoot<CountryKnowledgeDirectoryId>, IAuditableEntity, ISoftDeletable
{
    private const char LanguageSeparator = ',';

    private readonly List<CountryDirectoryEntry> _entries = [];
    private readonly List<CountryFeaturedGuide> _featuredGuides = [];
    private readonly List<CountryDirectoryLocalization> _localizations = [];

    private CountryKnowledgeDirectory(
        CountryKnowledgeDirectoryId id,
        string countryCode,
        string displayName,
        string? summary,
        string? primaryLanguage,
        string? languagesCsv,
        DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        DisplayName = displayName;
        Summary = summary;
        PrimaryLanguage = primaryLanguage;
        LanguagesCsv = languagesCsv;
        CreatedAt = createdAt;
    }

    private CountryKnowledgeDirectory() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Unique, and what every URL carries.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>The country's name as this platform writes it. Localizations override it.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The standfirst under the country's name. Editorial copy, rendered verbatim.</summary>
    public string? Summary { get; private set; }

    /// <summary>
    /// The language most guides for this country are written in.
    /// <para>
    /// A hint, not a filter. It tells a client which language tab to open on, and it tells the
    /// dashboard which language to count first. Guides in every other language remain fully
    /// discoverable — a directory that hid them would be worst for exactly the members who need it
    /// most.
    /// </para>
    /// </summary>
    public string? PrimaryLanguage { get; private set; }

    /// <summary>
    /// The other languages the country's knowledge is written in, comma-joined.
    /// <para>
    /// A delimited column rather than a fourth owned table, and the reason is that nothing ever
    /// queries it: it is a list of at most twenty two-letter codes that a client renders as a
    /// language switcher. B8 stores its SEO keywords the same way and for the same reason. The
    /// aggregate owns the delimiter so no caller ever sees one.
    /// </para>
    /// </summary>
    public string? LanguagesCsv { get; private set; }

    /// <summary>Whether readers can see the directory. Curators always can.</summary>
    public bool IsPublished { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>The optimistic concurrency token. Bumped on every mutation.</summary>
    public int Revision { get; private set; }

    /// <summary>The arranged navigation, in order.</summary>
    public IReadOnlyList<CountryDirectoryEntry> Entries => _entries.AsReadOnly();

    /// <summary>The curated shelf, across every scope.</summary>
    public IReadOnlyList<CountryFeaturedGuide> FeaturedGuides => _featuredGuides.AsReadOnly();

    public IReadOnlyList<CountryDirectoryLocalization> Localizations => _localizations.AsReadOnly();

    /// <summary>The declared languages, split. Empty rather than null when none were declared.</summary>
    public IReadOnlyList<string> Languages =>
        string.IsNullOrWhiteSpace(LanguagesCsv)
            ? []
            : [.. LanguagesCsv.Split(LanguageSeparator, StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)];

    /// <summary>The entries a reader sees: arranged, not hidden, in order.</summary>
    public IReadOnlyList<CountryDirectoryEntry> VisibleEntries =>
        [.. _entries.Where(entry => !entry.IsHidden).OrderBy(entry => entry.Position)];

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Construction
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<CountryKnowledgeDirectory> Create(
        CountryKnowledgeDirectoryId id,
        string? countryCode,
        string? displayName,
        DateTimeOffset now,
        string? summary = null,
        string? primaryLanguage = null,
        IEnumerable<string>? languages = null,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var country = KnowledgeNormalization.Country(countryCode);

        if (country is null)
        {
            return Result.Failure<CountryKnowledgeDirectory>(KnowledgeErrors.CountryInvalid);
        }

        // The country code is a defensible default name: a directory created by a template
        // contributor for a country nobody has written copy for yet is better than no directory.
        var name = KnowledgeNormalization.Text(displayName, KnowledgeLimits.MaxDirectoryName)
            ?? country;

        var joined = JoinLanguages(languages, out var languagesValid);

        if (!languagesValid)
        {
            return Result.Failure<CountryKnowledgeDirectory>(KnowledgeErrors.LocaleInvalid);
        }

        var directory = new CountryKnowledgeDirectory(
            id,
            country,
            name,
            KnowledgeNormalization.Text(summary, KnowledgeLimits.MaxDirectorySummary),
            KnowledgeNormalization.Locale(primaryLanguage),
            joined,
            now);

        directory.RaiseChanged(KnowledgeChangeKinds.Created, createdBy);

        return Result.Success(directory);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Metadata
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rewrites the header copy. The country code never changes — it is the identity.</summary>
    public Result UpdateMetadata(
        string? displayName,
        string? summary,
        string? primaryLanguage,
        IEnumerable<string>? languages,
        UserId? changedBy)
    {
        var name = KnowledgeNormalization.Text(displayName, KnowledgeLimits.MaxDirectoryName);

        if (name is null)
        {
            return Result.Failure(KnowledgeErrors.CategoryNameInvalid);
        }

        var joined = JoinLanguages(languages, out var languagesValid);

        if (!languagesValid)
        {
            return Result.Failure(KnowledgeErrors.LocaleInvalid);
        }

        DisplayName = name;
        Summary = KnowledgeNormalization.Text(summary, KnowledgeLimits.MaxDirectorySummary);
        PrimaryLanguage = KnowledgeNormalization.Locale(primaryLanguage);
        LanguagesCsv = joined;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Metadata, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the header translations. Same contract as the catalogue's.</summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? DisplayName, string? Summary)>? localizations,
        UserId? changedBy)
    {
        var requested = localizations?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxLocalizations)
        {
            return Result.Failure(KnowledgeErrors.LocalizationLimit);
        }

        var resolved = new List<CountryDirectoryLocalization>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, displayName, summary) in requested)
        {
            var localization = CountryDirectoryLocalization.TryCreate(locale, displayName, summary);

            if (localization is null)
            {
                return Result.Failure(KnowledgeErrors.LocaleInvalid);
            }

            if (seen.Add(localization.Locale))
            {
                resolved.Add(localization);
            }
        }

        _localizations.Clear();
        _localizations.AddRange(resolved);

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Metadata, changedBy);

        return Result.Success();
    }

    /// <summary>The header for a locale, falling back to the language and then to the default.</summary>
    public (string DisplayName, string? Summary) Localized(string? locale)
    {
        var normalized = KnowledgeNormalization.Locale(locale);

        if (normalized is null)
        {
            return (DisplayName, Summary);
        }

        var exact = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, normalized, StringComparison.Ordinal));

        if (exact is not null)
        {
            return (exact.DisplayName, exact.Summary ?? Summary);
        }

        var language = normalized.Split('-')[0];

        var byLanguage = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, language, StringComparison.Ordinal));

        return byLanguage is null
            ? (DisplayName, Summary)
            : (byLanguage.DisplayName, byLanguage.Summary ?? Summary);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Arrangement
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the whole arrangement, in the order given.
    /// <para>
    /// <b>Position comes from the order of the list, never from a number the caller supplies.</b> A
    /// caller-supplied position is a caller-supplied bug: two rows claiming position 3, a gap at 7
    /// after a delete, an ordering that renders differently from the list the curator dragged. The
    /// aggregate renumbers contiguously from zero on every save, so the stored order is always
    /// exactly what somebody arranged.
    /// </para>
    /// <para>
    /// A duplicated category is rejected rather than collapsed, because unlike a duplicated
    /// translation it is unambiguous evidence of a broken client: nobody drags the same row into a
    /// list twice.
    /// </para>
    /// </summary>
    public Result Arrange(
        IEnumerable<(string? KnowledgeCategoryKey, string? LabelOverride, bool IsHidden)>? entries,
        UserId? changedBy)
    {
        var requested = entries?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxDirectoryEntries)
        {
            return Result.Failure(KnowledgeErrors.EntryLimit);
        }

        var resolved = new List<CountryDirectoryEntry>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;

        foreach (var (key, labelOverride, isHidden) in requested)
        {
            var entry = CountryDirectoryEntry.TryCreate(key, position, labelOverride, isHidden);

            if (entry is null)
            {
                return Result.Failure(KnowledgeErrors.CategoryKeyInvalid);
            }

            if (!seen.Add(entry.KnowledgeCategoryKey))
            {
                return Result.Failure(KnowledgeErrors.EntryDuplicate);
            }

            resolved.Add(entry);
            position++;
        }

        _entries.Clear();
        _entries.AddRange(resolved);

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Arranged, changedBy);

        return Result.Success();
    }

    /// <summary>Whether the directory lists a category at all, hidden entries included.</summary>
    public bool Lists(string knowledgeCategoryKey) =>
        _entries.Any(entry => string.Equals(
            entry.KnowledgeCategoryKey, knowledgeCategoryKey, StringComparison.Ordinal));

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Curation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Features a guide on this country's shelf, optionally within one category.
    /// <para>
    /// The caller has already checked that the guide is published and about this country — the
    /// aggregate holds identifiers and cannot check either without reaching into B8, which is
    /// exactly the coupling the whole phase exists to avoid.
    /// </para>
    /// </summary>
    public Result FeatureGuide(
        Guid featureId,
        GuideId guideId,
        string? knowledgeCategoryKey,
        int? position,
        string? note,
        UserId featuredBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(guideId);
        ArgumentNullException.ThrowIfNull(featuredBy);

        if (_featuredGuides.Count >= KnowledgeLimits.MaxFeaturedGuides)
        {
            return Result.Failure(KnowledgeErrors.FeaturedLimit);
        }

        var candidate = CountryFeaturedGuide.TryCreate(
            featureId, guideId.Value, knowledgeCategoryKey, 0, note, now);

        if (candidate is null)
        {
            return Result.Failure(KnowledgeErrors.CategoryKeyInvalid);
        }

        var scope = candidate.KnowledgeCategoryKey;

        if (_featuredGuides.Any(featured => featured.Matches(guideId.Value, scope)))
        {
            return Result.Failure(KnowledgeErrors.AlreadyFeatured);
        }

        var shelf = ShelfOf(scope);
        var index = position is null ? shelf.Count : Math.Clamp(position.Value, 0, shelf.Count);

        shelf.Insert(index, candidate);

        _featuredGuides.Add(candidate);
        Renumber(shelf);

        Bump();

        RaiseDomainEvent(new CountryGuideFeatureChangedDomainEvent
        {
            DirectoryId = Id,
            CountryCode = CountryCode,
            GuideId = guideId,
            Featured = true,
            KnowledgeCategoryKey = scope,
            Position = candidate.Position,
            Note = candidate.Note,
            ChangedBy = featuredBy
        });

        return Result.Success();
    }

    /// <summary>Removes a guide from one shelf, leaving it published and discoverable.</summary>
    public Result UnfeatureGuide(
        GuideId guideId, string? knowledgeCategoryKey, UserId changedBy)
    {
        ArgumentNullException.ThrowIfNull(guideId);
        ArgumentNullException.ThrowIfNull(changedBy);

        var scope = string.IsNullOrWhiteSpace(knowledgeCategoryKey)
            ? null
            : KnowledgeNormalization.Key(knowledgeCategoryKey);

        var existing = _featuredGuides.FirstOrDefault(
            featured => featured.Matches(guideId.Value, scope));

        if (existing is null)
        {
            return Result.Failure(KnowledgeErrors.NotFeatured);
        }

        _featuredGuides.Remove(existing);
        Renumber(ShelfOf(scope));

        Bump();

        RaiseDomainEvent(new CountryGuideFeatureChangedDomainEvent
        {
            DirectoryId = Id,
            CountryCode = CountryCode,
            GuideId = guideId,
            Featured = false,
            KnowledgeCategoryKey = scope,
            Position = 0,
            Note = null,
            ChangedBy = changedBy
        });

        return Result.Success();
    }

    /// <summary>The featured guide identifiers for one scope, in curated order.</summary>
    public IReadOnlyList<Guid> FeaturedFor(string? knowledgeCategoryKey)
    {
        var scope = string.IsNullOrWhiteSpace(knowledgeCategoryKey)
            ? null
            : KnowledgeNormalization.Key(knowledgeCategoryKey);

        return
        [
            .. _featuredGuides
                .Where(featured => string.Equals(
                    featured.KnowledgeCategoryKey, scope, StringComparison.Ordinal))
                .OrderBy(featured => featured.Position)
                .Select(featured => featured.GuideId)
        ];
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result Publish(DateTimeOffset now, UserId? changedBy)
    {
        if (IsPublished)
        {
            return Result.Failure(KnowledgeErrors.DirectoryAlreadyPublished);
        }

        IsPublished = true;
        PublishedAt = now;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Published, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Takes the directory off the reader-facing surface without losing the arrangement.
    /// <para>
    /// <c>PublishedAt</c> is deliberately kept. It records when the country first went live, which
    /// is a fact about the platform's history and not a fact about the current state; clearing it
    /// would make a republished country look brand new to every consumer counting launches.
    /// </para>
    /// </summary>
    public Result Unpublish(UserId? changedBy)
    {
        if (!IsPublished)
        {
            return Result.Failure(KnowledgeErrors.DirectoryNotPublished);
        }

        IsPublished = false;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Archived, changedBy);

        return Result.Success();
    }

    /// <inheritdoc cref="KnowledgeCategory.EnsureRevision" />
    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(KnowledgeErrors.RevisionConflict);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Archives the directory. Called by the persistence pipeline from a repository removal, never
    /// by a handler.
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        // An archived directory is not a published one, whatever it was a moment ago. Leaving the
        // flag set would let a consumer that reads the archived row through a filtered-off query
        // conclude the country is live.
        IsPublished = false;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Archived, Actor(userId));
    }

    /// <summary>
    /// Returns an archived directory to the curator's desk — unpublished, arrangement intact.
    /// <para>
    /// Restoring deliberately does not republish. A country was archived for a reason, and putting
    /// it back in front of readers is a second decision somebody should have to make.
    /// </para>
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Restored, null);
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

    private void Bump() => Revision++;

    private List<CountryFeaturedGuide> ShelfOf(string? scope) =>
    [
        .. _featuredGuides
            .Where(featured => string.Equals(
                featured.KnowledgeCategoryKey, scope, StringComparison.Ordinal))
            .OrderBy(featured => featured.Position)
    ];

    private static void Renumber(List<CountryFeaturedGuide> shelf)
    {
        for (var index = 0; index < shelf.Count; index++)
        {
            shelf[index].MoveTo(index);
        }
    }

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new CountryDirectoryChangedDomainEvent
        {
            DirectoryId = Id,
            CountryCode = CountryCode,
            Change = change,
            IsPublished = IsPublished,
            CategoryCount = _entries.Count,
            ChangedBy = changedBy
        });

    /// <summary>
    /// Joins declared languages, reporting whether every one of them was usable.
    /// <para>
    /// An out parameter rather than a nullable return, because "no languages declared" and "a
    /// language tag was nonsense" are different answers that both produce no string, and treating
    /// them the same would silently drop a curator's whole list on one typo.
    /// </para>
    /// </summary>
    private static string? JoinLanguages(IEnumerable<string>? languages, out bool valid)
    {
        valid = true;

        var requested = languages?.ToList() ?? [];

        if (requested.Count == 0)
        {
            return null;
        }

        if (requested.Count > KnowledgeLimits.MaxDirectoryLanguages)
        {
            valid = false;
            return null;
        }

        var normalized = new List<string>(requested.Count);

        foreach (var language in requested)
        {
            var locale = KnowledgeNormalization.Locale(language);

            if (locale is null)
            {
                valid = false;
                return null;
            }

            if (!normalized.Contains(locale, StringComparer.Ordinal))
            {
                normalized.Add(locale);
            }
        }

        return string.Join(LanguageSeparator, normalized);
    }

    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
