using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Catalog;

/// <summary>
/// One subject area a member can have an opinion about.
/// <para>
/// <b>A row, not an enum, because the brief said "configurable" and meant it.</b> Billing and AI are
/// named as future categories; a deployment serving one country may want a category for immigration
/// updates that another has no use for. An enum would make each of those a code change, a migration
/// and a client release. A catalogue makes them a seeded row, contributed by whichever module cares
/// through <c>INotificationCategoryContributor</c>.
/// </para>
/// <para>
/// <b><see cref="IsMandatory"/> is the category-level expression of "some things cannot be switched
/// off".</b> Security is mandatory; the preference screen renders its switch disabled and says why,
/// rather than offering a control that silently does nothing.
/// </para>
/// <para>
/// Retired with a flag rather than deleted: notifications store the key, and deleting a category
/// would orphan every message ever filed under it.
/// </para>
/// </summary>
public sealed class NotificationCategory : AggregateRoot<NotificationCategoryId>, IAuditableEntity
{
    private NotificationCategory(
        NotificationCategoryId id,
        string key,
        string displayName,
        NotificationPriority defaultPriority,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        DisplayName = displayName;
        DefaultPriority = defaultPriority;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private NotificationCategory() { }

    /// <summary>Lower-cased, stable, and what every notification and preference stores.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// The words a client shows.
    /// <para>
    /// Held here rather than translated per language, deliberately. A category label is one or two
    /// words on a settings screen, the client already ships its own strings, and a localization table
    /// for sixteen nouns would be sixteen rows per language maintained by nobody. The response
    /// carries both the key and this label so a client can translate the key itself and fall back to
    /// this when it has no string for a category that shipped after it.
    /// </para>
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>What a rule gets when it does not state a priority of its own.</summary>
    public NotificationPriority DefaultPriority { get; private set; }

    /// <summary>Whether a member is allowed to mute it. False for security.</summary>
    public bool IsMandatory { get; private set; }

    /// <summary>Whether messages in this category are collected into digests by default.</summary>
    public bool DigestByDefault { get; private set; }

    /// <summary>Which module contributed it. Read by the manifest, never by the router.</summary>
    public string OwnerModule { get; private set; } = string.Empty;

    /// <summary>Lower sorts first on the preference screen.</summary>
    public int Position { get; private set; }

    public bool IsActive { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<NotificationCategory> Create(
        NotificationCategoryId id,
        string key,
        string displayName,
        string ownerModule,
        NotificationPriority defaultPriority,
        DateTimeOffset createdAt,
        string? description = null,
        bool isMandatory = false,
        bool digestByDefault = false,
        int position = 0)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (string.IsNullOrWhiteSpace(key) || key.Length > NotificationLimits.MaxKeyLength)
        {
            return Result.Failure<NotificationCategory>(NotificationErrors.PreferenceInvalid);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<NotificationCategory>(NotificationErrors.PreferenceInvalid);
        }

        return Result.Success(new NotificationCategory(
            id, key.Trim().ToLowerInvariant(), displayName.Trim(), defaultPriority, createdAt)
        {
            Description = description?.Trim(),
            IsMandatory = isMandatory,
            DigestByDefault = digestByDefault,
            OwnerModule = string.IsNullOrWhiteSpace(ownerModule) ? "Notifications" : ownerModule.Trim(),
            Position = Math.Max(0, position)
        });
    }

    /// <summary>
    /// Brings a seeded row up to date with what its contributor now declares.
    /// <para>
    /// Idempotent, because the seeder runs on every start. The key is never changed — it is the
    /// identity every stored notification and preference refers to.
    /// </para>
    /// </summary>
    public void Refresh(
        string displayName,
        string? description,
        NotificationPriority defaultPriority,
        bool isMandatory,
        bool digestByDefault,
        int position)
    {
        DisplayName = displayName.Trim();
        Description = description?.Trim();
        DefaultPriority = defaultPriority;
        IsMandatory = isMandatory;
        DigestByDefault = digestByDefault;
        Position = Math.Max(0, position);
        IsActive = true;

        Revision++;
    }

    public void Retire()
    {
        IsActive = false;
        Revision++;
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
}
