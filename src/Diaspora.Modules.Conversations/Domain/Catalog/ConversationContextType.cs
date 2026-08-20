using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.Catalog;

/// <summary>
/// One kind of thing a conversation can be about, as a catalogue row.
/// <para>
/// <b>Reference data, seeded by contributors, so a new vertical needs no change here.</b> B13 made
/// the same call for notification routing and the reasoning carries over exactly: the alternative is
/// an enum in this assembly that every vertical has to get itself added to, which makes the
/// Conversations module a bottleneck on every future phase and a deployment on every new noun.
/// </para>
/// <para>
/// <b>Rows carry behaviour, not just a label.</b> <see cref="RequiresAcceptance"/> is why the table
/// exists rather than a list of strings: a stranger opening a Marketplace enquiry about your listing
/// is expected and needs no gate, while a stranger opening a private conversation with you is
/// exactly what a message request is for. Encoding that per type in data means changing it is a
/// catalogue edit rather than a release.
/// </para>
/// </summary>
public sealed class ConversationContextType : Entity<string>, IAuditableEntity
{
    public const int MaxKeyLength = 64;
    public const int MaxDisplayNameLength = 120;
    public const int MaxDescriptionLength = 400;
    public const int MaxModuleLength = 64;

    private ConversationContextType(
        string key,
        string displayName,
        string? description,
        string? owningModule,
        bool requiresAcceptance,
        bool allowsAttachments,
        bool isSystemManaged,
        int position)
        : base(key)
    {
        DisplayName = displayName;
        Description = description;
        OwningModule = owningModule;
        RequiresAcceptance = requiresAcceptance;
        AllowsAttachments = allowsAttachments;
        IsSystemManaged = isSystemManaged;
        Position = position;
        IsActive = true;
    }

    private ConversationContextType() { }

    /// <summary>The client's literal — <c>MarketplaceListing</c>, not a slug.</summary>
    public string Key => Id;

    public string DisplayName { get; private set; } = default!;

    public string? Description { get; private set; }

    /// <summary>
    /// Which module's entity this points at, for operators and analytics.
    /// <para>
    /// Informational only. Nothing dispatches on it, and a type whose owning module is not deployed
    /// is a type with no conversations rather than a fault.
    /// </para>
    /// </summary>
    public string? OwningModule { get; private set; }

    /// <summary>Whether a conversation of this type starts as a request the recipient must accept.</summary>
    public bool RequiresAcceptance { get; private set; }

    public bool AllowsAttachments { get; private set; }

    /// <summary>
    /// Whether only the platform may open one.
    /// <para>
    /// A support ticket or a trust appeal is opened by the platform on a member's behalf; letting a
    /// client post one directly would let anybody manufacture a thread that looks official.
    /// </para>
    /// </summary>
    public bool IsSystemManaged { get; private set; }

    public int Position { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static ConversationContextType Create(
        string key,
        string displayName,
        string? description = null,
        string? owningModule = null,
        bool requiresAcceptance = false,
        bool allowsAttachments = true,
        bool isSystemManaged = false,
        int position = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new ConversationContextType(
            Clamp(key, MaxKeyLength)!,
            Clamp(displayName, MaxDisplayNameLength)!,
            Clamp(description, MaxDescriptionLength),
            Clamp(owningModule, MaxModuleLength),
            requiresAcceptance,
            allowsAttachments,
            isSystemManaged,
            position);
    }

    /// <summary>
    /// Re-applies a seed to an existing row.
    /// <para>
    /// The key is never touched — it is the identity, and conversations already reference it.
    /// Everything else is overwritten, because a seed is the contributor's current statement of what
    /// the type means and a half-applied seed is worse than either version.
    /// </para>
    /// </summary>
    public void Apply(
        string displayName,
        string? description,
        string? owningModule,
        bool requiresAcceptance,
        bool allowsAttachments,
        bool isSystemManaged,
        int position)
    {
        DisplayName = Clamp(displayName, MaxDisplayNameLength) ?? DisplayName;
        Description = Clamp(description, MaxDescriptionLength);
        OwningModule = Clamp(owningModule, MaxModuleLength);
        RequiresAcceptance = requiresAcceptance;
        AllowsAttachments = allowsAttachments;
        IsSystemManaged = isSystemManaged;
        Position = position;
        IsActive = true;
    }

    /// <summary>
    /// Takes the type out of the catalogue without removing it.
    /// <para>
    /// Deleting would orphan every conversation that references it. A deactivated type refuses new
    /// conversations and leaves existing ones readable, which is what retiring a vertical means.
    /// </para>
    /// </summary>
    public void Deactivate() => IsActive = false;

    private static string? Clamp(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max];
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
