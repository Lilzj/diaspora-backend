using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Templates;

/// <summary>
/// One authored message shape, in every language and on every channel it has.
/// <para>
/// <b>A template is the platform speaking in its own name, which is why publishing it is a
/// privileged act and editing a published one is refused.</b> A member receiving "your account has
/// been suspended" believes Diaspora wrote it. A draft that could be edited into production wording
/// without a second decision would be one typo away from telling two hundred thousand people
/// something untrue.
/// </para>
/// <para>
/// <b>Versioned by supersession, not by mutation.</b> Editing a published template creates a new
/// draft at the next version; the published one keeps serving until the new one is published in its
/// place. That is what makes a bad wording change recoverable without a deployment, and it is why
/// <see cref="Version"/> exists at all — nothing renders from a version number, but an operator
/// reading an incident timeline needs one.
/// </para>
/// <para>
/// <b>Transport-independent.</b> The template knows nothing about push payload limits, MIME parts or
/// SMS segments. It carries optional per-channel variants because some messages genuinely read
/// differently in an inbox and in a banner, and the channel decides what to do with what it is given.
/// An architecture test asserts this type has no dependency on any channel implementation.
/// </para>
/// </summary>
public sealed class NotificationTemplate : AggregateRoot<NotificationTemplateId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<TemplateLocalization> _localizations = [];
    private readonly List<TemplateChannelVariant> _variants = [];
    private readonly List<TemplateVariable> _variables = [];
    private readonly List<TemplateActionDefinition> _actions = [];

    private NotificationTemplate(
        NotificationTemplateId id,
        string key,
        int version,
        string categoryKey,
        NotificationPriority priority,
        string defaultLanguage,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        Version = version;
        CategoryKey = categoryKey;
        Priority = priority;
        DefaultLanguage = defaultLanguage;
        Status = TemplateStatus.Draft;
        CreatedAt = createdAt;
    }

    private NotificationTemplate() { }

    /// <summary>The stable name a rule refers to. Unique among templates that are not deactivated.</summary>
    public string Key { get; private set; } = string.Empty;

    public int Version { get; private set; }

    /// <summary>The catalogue key a message from this template is filed under.</summary>
    public string CategoryKey { get; private set; } = string.Empty;

    /// <summary>The default urgency. A rule may raise or lower it for a particular event.</summary>
    public NotificationPriority Priority { get; private set; }

    /// <summary>The language the template must always carry. Publication is refused without it.</summary>
    public string DefaultLanguage { get; private set; } = LocaleFallbackChain.DefaultLanguage;

    public TemplateStatus Status { get; private set; }

    /// <summary>An un-substituted client route for tapping the card itself.</summary>
    public string? RouteTemplate { get; private set; }

    /// <summary>What the author wrote it for. Never rendered; read by the next author.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// How long a message from this template stays in the inbox. Null means forever.
    /// <para>
    /// On the template rather than the rule because it is a property of the message's meaning: a
    /// viewing reminder is worthless the day after, and a verification approval is worth keeping.
    /// </para>
    /// </summary>
    public TimeSpan? RetentionPeriod { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<TemplateLocalization> Localizations => _localizations.AsReadOnly();

    public IReadOnlyList<TemplateChannelVariant> Variants => _variants.AsReadOnly();

    public IReadOnlyList<TemplateVariable> Variables => _variables.AsReadOnly();

    public IReadOnlyList<TemplateActionDefinition> Actions =>
        _actions.OrderBy(action => action.Position)
            .ThenBy(action => action.Key, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public bool IsUsable => Status == TemplateStatus.Published && !IsDeleted;

    public static Result<NotificationTemplate> Draft(
        NotificationTemplateId id,
        string key,
        string categoryKey,
        NotificationPriority priority,
        DateTimeOffset createdAt,
        int version = 1,
        string? defaultLanguage = null,
        string? description = null,
        string? routeTemplate = null,
        TimeSpan? retentionPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (string.IsNullOrWhiteSpace(key) || key.Length > NotificationLimits.MaxKeyLength)
        {
            return Result.Failure<NotificationTemplate>(NotificationErrors.TemplateInvalid);
        }

        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return Result.Failure<NotificationTemplate>(NotificationErrors.CategoryNotFound);
        }

        var template = new NotificationTemplate(
            id,
            key.Trim().ToLowerInvariant(),
            Math.Max(1, version),
            categoryKey.Trim().ToLowerInvariant(),
            priority,
            string.IsNullOrWhiteSpace(defaultLanguage)
                ? LocaleFallbackChain.DefaultLanguage
                : LocaleFallbackChain.Normalize(defaultLanguage),
            createdAt)
        {
            Description = description?.Trim(),
            RouteTemplate = routeTemplate?.Trim(),
            RetentionPeriod = retentionPeriod
        };

        return Result.Success(template);
    }

    /// <summary>
    /// Adds or replaces one language's words.
    /// <para>
    /// Allowed on a published template, deliberately and as the single exception to "published is
    /// frozen". Adding Yoruba to a template that already ships in English changes nothing anybody
    /// currently receives and is the difference between a translation landing this week and waiting
    /// for a whole new version to be published. Changing existing wording still requires a new draft.
    /// </para>
    /// </summary>
    public Result SetLocalization(TemplateLocalization localization)
    {
        ArgumentNullException.ThrowIfNull(localization);

        if (Status == TemplateStatus.Deactivated)
        {
            return Result.Failure(NotificationErrors.TemplateNotDraft);
        }

        var index = _localizations.FindIndex(existing =>
            string.Equals(existing.Language, localization.Language, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            if (Status == TemplateStatus.Published)
            {
                // Replacing wording that is already being sent is a version, not an edit.
                return Result.Failure(NotificationErrors.TemplateNotDraft);
            }

            _localizations[index] = localization;
        }
        else
        {
            if (_localizations.Count >= NotificationLimits.MaxLocalizations)
            {
                return Result.Failure(NotificationErrors.TemplateInvalid);
            }

            _localizations.Add(localization);
        }

        Touch();

        return Result.Success();
    }

    public Result SetVariant(TemplateChannelVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);

        if (Status != TemplateStatus.Draft)
        {
            return Result.Failure(NotificationErrors.TemplateNotDraft);
        }

        var index = _variants.FindIndex(existing =>
            existing.Channel == variant.Channel
            && string.Equals(existing.Language, variant.Language, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            _variants[index] = variant;
        }
        else
        {
            _variants.Add(variant);
        }

        Touch();

        return Result.Success();
    }

    public Result SetVariables(IEnumerable<TemplateVariable> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        if (Status != TemplateStatus.Draft)
        {
            return Result.Failure(NotificationErrors.TemplateNotDraft);
        }

        var declared = variables.Take(NotificationLimits.MaxTemplateVariables).ToList();

        _variables.Clear();
        _variables.AddRange(declared);

        Touch();

        return Result.Success();
    }

    public Result SetActions(IEnumerable<TemplateActionDefinition> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (Status != TemplateStatus.Draft)
        {
            return Result.Failure(NotificationErrors.TemplateNotDraft);
        }

        _actions.Clear();
        _actions.AddRange(actions.Take(NotificationLimits.MaxActions));

        Touch();

        return Result.Success();
    }

    public Result UpdateMetadata(
        string? description,
        string? routeTemplate,
        NotificationPriority? priority,
        TimeSpan? retentionPeriod)
    {
        if (Status != TemplateStatus.Draft)
        {
            return Result.Failure(NotificationErrors.TemplateNotDraft);
        }

        Description = description?.Trim();
        RouteTemplate = routeTemplate?.Trim();

        if (priority is { } value)
        {
            Priority = value;
        }

        RetentionPeriod = retentionPeriod;

        Touch();

        return Result.Success();
    }

    /// <summary>
    /// Makes the template usable.
    /// <para>
    /// The three checks here are the whole of template validation, and each has a failure mode
    /// somebody would otherwise discover in production: no default language means a member outside
    /// the translated set receives nothing; an undeclared variable in the body means a message with
    /// a literal <c>{jobTitle}</c> in it; and HTML content means an unsanitised script tag in an
    /// email, which is the reason <see cref="ContentFormat.Html"/> is declared and refused.
    /// </para>
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (Status == TemplateStatus.Published)
        {
            return Result.Success();
        }

        if (Status == TemplateStatus.Deactivated)
        {
            return Result.Failure(NotificationErrors.TemplateNotDraft);
        }

        var validation = Validate();

        if (validation.IsFailure)
        {
            return validation;
        }

        Status = TemplateStatus.Published;
        PublishedAt = now;
        DeactivatedAt = null;

        Touch();

        RaiseDomainEvent(new TemplatePublishedDomainEvent
        {
            TemplateId = Id,
            TemplateKey = Key,
            Version = Version,
            PublishedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Everything publication refuses, in one place so the authoring screen can call it too.
    /// </summary>
    public Result Validate()
    {
        var hasDefault = _localizations.Exists(localization =>
            string.Equals(localization.Language, DefaultLanguage, StringComparison.OrdinalIgnoreCase));

        if (!hasDefault)
        {
            return Result.Failure(NotificationErrors.TemplateLocaleMissing);
        }

        if (_localizations.Exists(localization => localization.Format == ContentFormat.Html)
            || _variants.Exists(variant => variant.Format == ContentFormat.Html))
        {
            return Result.Failure(NotificationErrors.TemplateInvalid);
        }

        var declared = _variables
            .Select(variable => variable.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var text in EveryRenderableString())
        {
            foreach (var token in TemplateTokenScanner.Scan(text))
            {
                if (!declared.Contains(token))
                {
                    return Result.Failure(NotificationErrors.TemplateVariableUnknown);
                }
            }
        }

        return Result.Success();
    }

    /// <summary>Retires the template. Messages already sent from it are untouched.</summary>
    public Result Deactivate(DateTimeOffset now)
    {
        if (Status == TemplateStatus.Deactivated)
        {
            return Result.Success();
        }

        Status = TemplateStatus.Deactivated;
        DeactivatedAt = now;

        Touch();

        RaiseDomainEvent(new TemplateDeactivatedDomainEvent
        {
            TemplateId = Id,
            TemplateKey = Key,
            DeactivatedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// The next draft of this template, ready to be edited and published in its place.
    /// <para>
    /// A copy rather than a mutation, which is what makes a wording change reversible: the current
    /// version keeps serving until the new one is published, and if the new wording turns out to be
    /// wrong the old one is still there to publish again.
    /// </para>
    /// </summary>
    public Result<NotificationTemplate> CreateNextVersion(
        NotificationTemplateId id, DateTimeOffset createdAt)
    {
        var draft = Draft(
            id,
            Key,
            CategoryKey,
            Priority,
            createdAt,
            Version + 1,
            DefaultLanguage,
            Description,
            RouteTemplate,
            RetentionPeriod);

        if (draft.IsFailure)
        {
            return draft;
        }

        draft.Value._localizations.AddRange(_localizations);
        draft.Value._variants.AddRange(_variants);
        draft.Value._variables.AddRange(_variables);
        draft.Value._actions.AddRange(_actions);

        return draft;
    }

    // ── Resolution the renderer uses ────────────────────────────────────────────────────────

    /// <summary>
    /// The words to use for a language, walking the fallback chain.
    /// <para>
    /// Null only when the template has no localizations at all, which publication refuses — so a
    /// published template always answers.
    /// </para>
    /// </summary>
    public TemplateLocalization? ResolveLocalization(string? language)
    {
        foreach (var candidate in LocaleFallbackChain.For(language))
        {
            var match = _localizations.Find(localization =>
                string.Equals(localization.Language, candidate, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return _localizations.Find(localization =>
                   string.Equals(localization.Language, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
               ?? _localizations.FirstOrDefault();
    }

    /// <summary>The channel override for a language, or null to use the localization as written.</summary>
    public TemplateChannelVariant? ResolveVariant(NotificationChannelKind channel, string? language)
    {
        foreach (var candidate in LocaleFallbackChain.For(language))
        {
            var match = _variants.Find(variant =>
                variant.Channel == channel
                && string.Equals(variant.Language, candidate, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        Touch();
    }

    private IEnumerable<string> EveryRenderableString()
    {
        foreach (var localization in _localizations)
        {
            yield return localization.Title;
            yield return localization.Body;

            if (localization.ShortBody is { } shortBody)
            {
                yield return shortBody;
            }

            if (localization.PluralBody is { } pluralBody)
            {
                yield return pluralBody;
            }
        }

        foreach (var variant in _variants)
        {
            if (variant.Title is { } title)
            {
                yield return title;
            }

            if (variant.Body is { } body)
            {
                yield return body;
            }
        }

        if (RouteTemplate is { } route)
        {
            yield return route;
        }

        foreach (var action in _actions)
        {
            if (action.RouteTemplate is { } actionRoute)
            {
                yield return actionRoute;
            }
        }
    }

    private void Touch() => Revision++;
}
