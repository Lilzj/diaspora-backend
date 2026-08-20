using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Knowledge;

/// <summary>
/// One row of the Country Knowledge Directory's navigation — "Immigration", "Housing",
/// "Cost of Living".
/// <para>
/// <b>This is not a second guide taxonomy, and the distinction is the architectural claim of
/// B8.1.</b> A guide is filed under a <c>GuideCategory</c>, by its author, at the moment it is
/// written; that has been true since B8 and stays true. A knowledge category is what a country
/// page <em>shows</em>: which rows appear, in what order, under what localized label, drawing from
/// which filing categories, with which kinds of professional recommended beside them. Filing is
/// content and belongs to the author. Navigation is curation and belongs to an editor. Conflating
/// them is how a knowledge base ends up unable to rename a menu item without re-filing ten
/// thousand guides.
/// </para>
/// <para>
/// <b>The key is the contract and never changes.</b> It is what a contributor seed declares, what
/// a URL carries, what a country directory entry points at and what a client switches on. Renaming
/// is free; rekeying is not offered.
/// </para>
/// <para>
/// <b>Archiving is the soft delete, and there is deliberately only one "gone" state.</b> Carrying
/// both an <c>IsActive</c> flag and a soft delete would give two ways for a category to be absent,
/// two queries that disagree about which, and a restore path that fixes one of them. Archiving
/// removes the node through the platform's ordinary removal path, the shared query filter hides it
/// everywhere at once, and restoring is the documented undelete — read through the repository
/// member that sees past the filter, because the row a restore needs is the row the filter hid.
/// </para>
/// </summary>
public sealed class KnowledgeCategory
    : AggregateRoot<KnowledgeCategoryId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<KnowledgeCategoryLocalization> _localizations = [];
    private readonly List<KnowledgeCategorySource> _sources = [];
    private readonly List<KnowledgeProviderCategory> _providerCategories = [];

    private KnowledgeCategory(
        KnowledgeCategoryId id,
        string key,
        string name,
        string? description,
        string? icon,
        int order,
        DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        Icon = icon;
        Order = order;
        CreatedAt = createdAt;
    }

    private KnowledgeCategory() { }

    /// <summary>The stable, seedable identity. Lower-case kebab, unique across the catalogue.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The default label, in the platform's own language. Localizations override it.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>An emoji or icon name. Opaque to the server — the client decides what it is.</summary>
    public string? Icon { get; private set; }

    /// <summary>
    /// The catalogue-wide default position. A country directory may override it per country, and
    /// most do not — which is why the default is worth having.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// The optimistic concurrency token, bumped on every mutation.
    /// <para>
    /// Curation is the one part of this platform where simultaneous editors are the norm rather
    /// than the exception: a category catalogue is edited by a small team, all of them at once,
    /// during the week a new country launches. Without this, the second save silently wins.
    /// </para>
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>Human translations of the label and the description. Never machine-generated.</summary>
    public IReadOnlyList<KnowledgeCategoryLocalization> Localizations => _localizations.AsReadOnly();

    /// <summary>
    /// The guide categories this row draws content from. Empty means "the guide category whose
    /// key matches mine", which is the common case and costs no rows.
    /// </summary>
    public IReadOnlyList<KnowledgeCategorySource> Sources => _sources.AsReadOnly();

    /// <summary>Provider service categories recommended beside this subject.</summary>
    public IReadOnlyList<KnowledgeProviderCategory> ProviderCategories =>
        _providerCategories.AsReadOnly();

    /// <summary>Whether the category may still be placed on a directory. The inverse of archived.</summary>
    public bool IsActive => !IsDeleted;

    /// <summary>
    /// The guide category keys this row resolves to, falling back to its own key.
    /// <para>
    /// The single place the fallback is expressed. A discovery query asks for this and never
    /// reasons about whether the sources collection is empty, which is how the two would drift.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> EffectiveSourceKeys =>
        _sources.Count == 0
            ? [Key]
            : [.. _sources.Select(source => source.GuideCategoryKey)];

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

    public static Result<KnowledgeCategory> Create(
        KnowledgeCategoryId id,
        string? key,
        string? name,
        DateTimeOffset now,
        string? description = null,
        string? icon = null,
        int order = 0,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = KnowledgeNormalization.Key(key);

        if (normalizedKey is null)
        {
            return Result.Failure<KnowledgeCategory>(KnowledgeErrors.CategoryKeyInvalid);
        }

        var trimmedName = KnowledgeNormalization.Text(name, KnowledgeLimits.MaxCategoryName);

        if (trimmedName is null)
        {
            return Result.Failure<KnowledgeCategory>(KnowledgeErrors.CategoryNameInvalid);
        }

        var category = new KnowledgeCategory(
            id,
            normalizedKey,
            trimmedName,
            KnowledgeNormalization.Text(description, KnowledgeLimits.MaxCategoryDescription),
            KnowledgeNormalization.Text(icon, KnowledgeLimits.MaxIcon),
            order,
            now);

        category.RaiseChanged(KnowledgeChangeKinds.Created, createdBy);

        return Result.Success(category);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Curation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Renames or re-describes the row. The key never changes — it is the contract.</summary>
    public Result Update(string? name, string? description, string? icon, int order, UserId? changedBy)
    {
        var trimmedName = KnowledgeNormalization.Text(name, KnowledgeLimits.MaxCategoryName);

        if (trimmedName is null)
        {
            return Result.Failure(KnowledgeErrors.CategoryNameInvalid);
        }

        Name = trimmedName;
        Description = KnowledgeNormalization.Text(description, KnowledgeLimits.MaxCategoryDescription);
        Icon = KnowledgeNormalization.Text(icon, KnowledgeLimits.MaxIcon);
        Order = order;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the whole source set.
    /// <para>
    /// Replace rather than add-and-remove, because the caller is a curation screen that already
    /// holds the intended list, and a differ on this side would only be a second implementation of
    /// the one the screen has. Duplicates collapse; unusable keys are rejected outright rather than
    /// dropped, so a curator never gets a silent partial save.
    /// </para>
    /// </summary>
    public Result SetSources(IEnumerable<string>? guideCategoryKeys, UserId? changedBy)
    {
        var requested = guideCategoryKeys?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxCategorySources)
        {
            return Result.Failure(KnowledgeErrors.SourceLimit);
        }

        var resolved = new List<KnowledgeCategorySource>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in requested)
        {
            var source = KnowledgeCategorySource.TryCreate(key);

            if (source is null)
            {
                return Result.Failure(KnowledgeErrors.CategoryKeyInvalid);
            }

            if (seen.Add(source.GuideCategoryKey))
            {
                resolved.Add(source);
            }
        }

        _sources.Clear();
        _sources.AddRange(resolved);

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>Replaces the recommended provider service categories. Same contract as the sources.</summary>
    public Result SetProviderCategories(IEnumerable<string>? serviceCategoryKeys, UserId? changedBy)
    {
        var requested = serviceCategoryKeys?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxProviderCategories)
        {
            return Result.Failure(KnowledgeErrors.SourceLimit);
        }

        var resolved = new List<KnowledgeProviderCategory>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in requested)
        {
            var category = KnowledgeProviderCategory.TryCreate(key);

            if (category is null)
            {
                return Result.Failure(KnowledgeErrors.CategoryKeyInvalid);
            }

            if (seen.Add(category.ServiceCategoryKey))
            {
                resolved.Add(category);
            }
        }

        _providerCategories.Clear();
        _providerCategories.AddRange(resolved);

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the localization set.
    /// <para>
    /// A locale that appears twice keeps its first entry rather than failing, because the ordinary
    /// cause is a curator pasting a spreadsheet with a duplicated row, and rejecting forty
    /// translations over one repeated line helps nobody. An unusable locale tag <em>does</em> fail,
    /// because that one is a typo the curator can fix and would otherwise silently lose a language.
    /// </para>
    /// </summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? Name, string? Description)>? localizations,
        UserId? changedBy)
    {
        var requested = localizations?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxLocalizations)
        {
            return Result.Failure(KnowledgeErrors.LocalizationLimit);
        }

        var resolved = new List<KnowledgeCategoryLocalization>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, name, description) in requested)
        {
            var localization = KnowledgeCategoryLocalization.TryCreate(
                locale,
                name,
                description,
                KnowledgeLimits.MaxCategoryName,
                KnowledgeLimits.MaxCategoryDescription);

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
        RaiseChanged(KnowledgeChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// The label for a locale, falling back to the default name.
    /// <para>
    /// Falls back on the language alone before giving up — a reader asking for <c>fr-MU</c> gets
    /// the <c>fr</c> translation if nobody has written a Mauritian French one, which is right far
    /// more often than showing them English.
    /// </para>
    /// </summary>
    public (string Name, string? Description) Localized(string? locale)
    {
        var normalized = KnowledgeNormalization.Locale(locale);

        if (normalized is null)
        {
            return (Name, Description);
        }

        var exact = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, normalized, StringComparison.Ordinal));

        if (exact is not null)
        {
            return (exact.Name, exact.Description ?? Description);
        }

        var language = normalized.Split('-')[0];

        var byLanguage = _localizations.FirstOrDefault(
            localization => string.Equals(localization.Locale, language, StringComparison.Ordinal));

        return byLanguage is null
            ? (Name, Description)
            : (byLanguage.Name, byLanguage.Description ?? Description);
    }

    /// <summary>
    /// Rejects a write whose caller was looking at an older revision.
    /// <para>
    /// The aggregate-level half of optimistic concurrency; the concurrency token on the column is
    /// the other. This one catches the editor who sends back a stale revision and can be told
    /// precisely what happened; the token catches the one who sends none.
    /// </para>
    /// </summary>
    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(KnowledgeErrors.RevisionConflict);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Archives the category. Called by the persistence pipeline from a repository removal, never
    /// by a handler — the same rule B1.0.1 records for every soft delete on the platform.
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Archived, Actor(userId));
    }

    /// <summary>Returns an archived category to the catalogue.</summary>
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

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new KnowledgeCategoryChangedDomainEvent
        {
            KnowledgeCategoryId = Id,
            Key = Key,
            Name = Name,
            Change = change,
            IsActive = IsActive,
            SourceCategoryKeys = EffectiveSourceKeys,
            ChangedBy = changedBy
        });

    /// <summary>
    /// The audit interceptor hands the actor down as a string, because that is what it stamps into
    /// the row. The event wants an identifier. A caller that is not a member — a seeder, a
    /// background sweep — leaves it null rather than inventing one.
    /// </summary>
    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
