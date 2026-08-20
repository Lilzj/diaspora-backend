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
/// A kind of official notice — <c>visa-advisories</c>, <c>emergency-alerts</c>,
/// <c>public-holidays</c>.
/// <para>
/// <b>The third category concept in this module, and the three are genuinely different
/// questions.</b> A <c>GuideCategory</c> is where an author files something they wrote. A
/// <c>KnowledgeCategory</c> is a row a curator puts on a country page. This is the kind of thing an
/// authority issued — which decides how urgent it is by default, which extra contract its
/// publication produces, and what a member is choosing when they turn a notification off. Merging
/// any two of them would make one of those three answers unavailable.
/// </para>
/// <para>
/// <b>The category carries the alert family, and that is the whole reason it is an aggregate rather
/// than an enum.</b> A deployment that adds "Cyclone Warnings" needs the platform to treat it as an
/// emergency, and a hard-coded list inside the relay would mean it does not — a fact the deployment
/// discovers during a cyclone. Declaring it here makes the decision editorial and changeable without
/// a release.
/// </para>
/// <para>
/// <b>The key is the contract and never changes.</b> It is what a seed declares, what a URL carries,
/// what every notice is filed under and what a member's notification preference stores. Renaming is
/// free; rekeying is not offered. Archiving is the soft delete, one gone state, restorable through
/// the repository member that sees past the query filter.
/// </para>
/// </summary>
public sealed class NoticeCategory : AggregateRoot<NoticeCategoryId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<NoticeCategoryLocalization> _localizations = [];

    private NoticeCategory(
        NoticeCategoryId id,
        string key,
        string name,
        string? description,
        string? icon,
        int order,
        NoticeSeverity defaultSeverity,
        NoticeAlertFamily alertFamily,
        DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        Icon = icon;
        Order = order;
        DefaultSeverity = defaultSeverity;
        AlertFamily = alertFamily;
        CreatedAt = createdAt;
    }

    private NoticeCategory() { }

    /// <summary>The stable, seedable identity. Lower-case kebab, unique across the catalogue.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The default label, in the platform's own language. Localizations override it.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>An emoji or icon name. Opaque to the server — the client decides what it is.</summary>
    public string? Icon { get; private set; }

    /// <summary>Catalogue-wide position. Lower first.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// What a notice in this category is worth before an editor decides otherwise.
    /// <para>
    /// A default rather than a fixed value, because the category says what kind of thing this is and
    /// the editor says how bad this particular one is. A passport-service notice is usually
    /// informational and is occasionally the announcement that the office closes on Friday.
    /// </para>
    /// </summary>
    public NoticeSeverity DefaultSeverity { get; private set; }

    /// <summary>Which extra published contract this category's notices produce.</summary>
    public NoticeAlertFamily AlertFamily { get; private set; }

    /// <summary>The optimistic concurrency token, bumped on every mutation.</summary>
    public int Revision { get; private set; }

    /// <summary>Human translations of the label and the description. Never machine-generated.</summary>
    public IReadOnlyList<NoticeCategoryLocalization> Localizations => _localizations.AsReadOnly();

    /// <summary>Whether notices may still be filed under it. The inverse of archived.</summary>
    public bool IsActive => !IsDeleted;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Construction and curation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<NoticeCategory> Create(
        NoticeCategoryId id,
        string? key,
        string? name,
        DateTimeOffset now,
        string? description = null,
        string? icon = null,
        int order = 0,
        NoticeSeverity defaultSeverity = NoticeSeverity.Informational,
        NoticeAlertFamily alertFamily = NoticeAlertFamily.None,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = IntelligenceNormalization.Key(key);

        if (normalizedKey is null)
        {
            return Result.Failure<NoticeCategory>(IntelligenceErrors.CategoryKeyInvalid);
        }

        var trimmedName = IntelligenceNormalization.Text(
            name, IntelligenceLimits.MaxCategoryName);

        if (trimmedName is null)
        {
            return Result.Failure<NoticeCategory>(IntelligenceErrors.CategoryNameInvalid);
        }

        if (!Enum.IsDefined(defaultSeverity))
        {
            return Result.Failure<NoticeCategory>(IntelligenceErrors.SeverityInvalid);
        }

        var category = new NoticeCategory(
            id,
            normalizedKey,
            trimmedName,
            IntelligenceNormalization.Text(description, IntelligenceLimits.MaxCategoryDescription),
            IntelligenceNormalization.Text(icon, KnowledgeLimits.MaxIcon),
            order,
            defaultSeverity,
            Enum.IsDefined(alertFamily) ? alertFamily : NoticeAlertFamily.None,
            now);

        category.RaiseChanged(IntelligenceChangeKinds.Created, createdBy);

        return Result.Success(category);
    }

    /// <summary>Rewrites everything except the key, which is the contract.</summary>
    public Result Update(
        string? name,
        string? description,
        string? icon,
        int order,
        NoticeSeverity defaultSeverity,
        NoticeAlertFamily alertFamily,
        UserId? changedBy)
    {
        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxCategoryName);

        if (trimmedName is null)
        {
            return Result.Failure(IntelligenceErrors.CategoryNameInvalid);
        }

        if (!Enum.IsDefined(defaultSeverity) || !Enum.IsDefined(alertFamily))
        {
            return Result.Failure(IntelligenceErrors.SeverityInvalid);
        }

        Name = trimmedName;
        Description = IntelligenceNormalization.Text(
            description, IntelligenceLimits.MaxCategoryDescription);
        Icon = IntelligenceNormalization.Text(icon, KnowledgeLimits.MaxIcon);
        Order = order;
        DefaultSeverity = defaultSeverity;
        AlertFamily = alertFamily;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the localization set.
    /// <para>
    /// A locale that appears twice keeps its first entry rather than failing — the ordinary cause is
    /// a pasted spreadsheet with a repeated row. An unusable locale tag does fail, because that one
    /// is a typo somebody can fix and would otherwise silently lose a language.
    /// </para>
    /// </summary>
    public Result SetLocalizations(
        IEnumerable<(string? Locale, string? Name, string? Description)>? localizations,
        UserId? changedBy)
    {
        var requested = localizations?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxLocalizations)
        {
            return Result.Failure(IntelligenceErrors.LocalizationLimit);
        }

        var resolved = new List<NoticeCategoryLocalization>(requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, name, description) in requested)
        {
            var localization = NoticeCategoryLocalization.TryCreate(locale, name, description);

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

    /// <summary>
    /// The label for a locale, falling back through the language to the default.
    /// <para>
    /// The same exact→language→default ladder every localized read in this module uses.
    /// </para>
    /// </summary>
    public (string Name, string? Description) Localized(string? locale)
    {
        var normalized = IntelligenceNormalization.Locale(locale);

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

    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(IntelligenceErrors.RevisionConflict);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete and auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Archives the category. Called by the persistence pipeline from a repository removal, never by
    /// a handler — the rule B1.0.1 records for every soft delete on this platform.
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        Bump();
        RaiseChanged(IntelligenceChangeKinds.Archived, Actor(userId));
    }

    /// <summary>Returns an archived category to the catalogue.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

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

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new NoticeCategoryChangedDomainEvent
        {
            NoticeCategoryId = Id,
            Key = Key,
            Name = Name,
            Change = change,
            IsActive = IsActive,
            DefaultSeverity = DefaultSeverity,
            ChangedBy = changedBy
        });

    private static UserId? Actor(string? userId) =>
        Guid.TryParse(userId, out var parsed) ? UserId.From(parsed) : null;
}
