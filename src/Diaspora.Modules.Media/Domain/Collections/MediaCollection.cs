using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.Collections;

/// <summary>
/// An ordered set of assets that belongs to one owning entity: a listing's gallery, a
/// message's attachments, a provider's four photographs.
/// <para>
/// <b>Why order lives here rather than on the client.</b> Every gallery in the app today holds
/// its order implicitly, as array position in React state — which means the order is lost on
/// any refetch, never reaches a second device, and cannot be reasoned about by the server at
/// all. A member who spends five minutes arranging listing photographs and then pulls to
/// refresh loses the arrangement. Storing an explicit position is what makes the arrangement
/// real, and the response array is still ordered, so the client's <c>.map()</c> is unchanged.
/// </para>
/// <para>
/// Cardinality is a property of the collection's kind rather than of each caller. A provider
/// gallery holds four because the wizard renders four slots; message attachments hold five
/// because the picker's <c>selectionLimit</c> is five. Encoding those against the kind is what
/// stops the third caller inventing a sixth number.
/// </para>
/// </summary>
public sealed class MediaCollection : AggregateRoot<MediaCollectionId>, IAuditableEntity
{
    public const int MaxNameLength = 200;

    private readonly List<MediaCollectionItem> _items = [];

    public MediaCollectionKind Kind { get; private set; }

    public MediaOwner Owner { get; private set; } = MediaOwner.Unassigned;

    public UserId CreatedByUser { get; private set; } = default!;

    public string? Name { get; private set; }

    /// <summary>
    /// A denormalised <c>type:id:kind</c> key, carrying the one-collection-per-owner-and-kind
    /// rule into the database as a unique index.
    /// <para>
    /// Denormalised because the rule spans an owned value object and a property of the
    /// aggregate, and no relational index can span both — an owned type's columns belong to
    /// the owned entity even though they share the table. The alternative was to enforce
    /// uniqueness only in the handler, which holds until two concurrent requests create the
    /// same gallery and a listing ends up with two.
    /// </para>
    /// <para>
    /// Set once, at construction, and never mutated: every component of it is immutable on the
    /// aggregate, so it cannot go stale.
    /// </para>
    /// </summary>
    public string OwnerKey { get; private set; } = string.Empty;

    public IReadOnlyList<MediaCollectionItem> Items => _items.AsReadOnly();

    /// <summary>The items in the order they should be rendered.</summary>
    public IReadOnlyList<MediaCollectionItem> OrderedItems =>
        _items.OrderBy(item => item.Position).ToList().AsReadOnly();

    /// <summary>The first item — the cover, the primary photo, the thumbnail of the set.</summary>
    public MediaCollectionItem? Primary =>
        _items.OrderBy(item => item.Position).FirstOrDefault();

    public int Capacity => MediaLimits.MaxItemsFor(Kind);

    public bool IsFull => _items.Count >= Capacity;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private MediaCollection() { }

    private MediaCollection(
        MediaCollectionId id,
        MediaCollectionKind kind,
        MediaOwner owner,
        UserId createdByUser,
        string? name) : base(id)
    {
        Kind = kind;
        Owner = owner;
        CreatedByUser = createdByUser;
        Name = name;
        OwnerKey = BuildOwnerKey(owner, kind);
    }

    /// <summary>The stable key the unique index is built on.</summary>
    public static string BuildOwnerKey(MediaOwner owner, MediaCollectionKind kind)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return $"{(int)owner.Type}:{owner.Id:N}:{(int)kind}";
    }

    public static Result<MediaCollection> Create(
        MediaCollectionKind kind, MediaOwner owner, UserId createdByUser, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(createdByUser);

        return Result.Success(new MediaCollection(
            MediaCollectionId.New(), kind, owner, createdByUser, Truncate(name, MaxNameLength)));
    }

    /// <summary>
    /// Appends an asset.
    /// <para>
    /// Adding an asset already in the collection is a no-op rather than an error: a client
    /// retrying a request whose response it lost must not be told it did something wrong.
    /// </para>
    /// </summary>
    public Result<MediaCollectionItem> Add(MediaId mediaId, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        var existing = _items.FirstOrDefault(item => item.MediaId == mediaId);

        if (existing is not null)
        {
            return Result.Success(existing);
        }

        if (IsFull)
        {
            return Result.Failure<MediaCollectionItem>(MediaErrors.CollectionFull(Capacity));
        }

        var position = _items.Count == 0 ? 0 : _items.Max(item => item.Position) + 1;
        var item = MediaCollectionItem.Create(Id, mediaId, position, caption);

        _items.Add(item);

        return Result.Success(item);
    }

    /// <summary>
    /// Removes an asset and closes the gap it left.
    /// <para>
    /// Re-sequencing rather than leaving a hole. Positions with gaps still render in the right
    /// order, but every subsequent reorder has to reason about them, and the first caller that
    /// treats position as an array index gets it wrong.
    /// </para>
    /// </summary>
    public Result Remove(MediaId mediaId)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        var item = _items.FirstOrDefault(candidate => candidate.MediaId == mediaId);

        if (item is null)
        {
            return Result.Failure(MediaErrors.CollectionItemNotFound);
        }

        _items.Remove(item);
        Resequence();

        return Result.Success();
    }

    /// <summary>
    /// Sets the order outright, from a complete list of the collection's assets.
    /// <para>
    /// All-or-nothing, and it refuses a list that is not exactly the collection's membership.
    /// A partial reorder has no defined meaning — a client sending three of five ids has not
    /// said where the other two go — and accepting one would produce an order neither side
    /// agrees on.
    /// </para>
    /// </summary>
    public Result Reorder(IReadOnlyList<MediaId> orderedMediaIds)
    {
        ArgumentNullException.ThrowIfNull(orderedMediaIds);

        var requested = orderedMediaIds.Select(id => id.Value).ToList();

        if (requested.Count != _items.Count ||
            requested.Distinct().Count() != requested.Count ||
            !requested.All(id => _items.Any(item => item.MediaId.Value == id)))
        {
            return Result.Failure(MediaErrors.CollectionItemNotFound);
        }

        for (var position = 0; position < requested.Count; position++)
        {
            var item = _items.First(candidate => candidate.MediaId.Value == requested[position]);
            item.MoveTo(position);
        }

        return Result.Success();
    }

    /// <summary>Promotes one asset to the front — "make this the cover photo".</summary>
    public Result MakePrimary(MediaId mediaId)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        if (_items.All(item => item.MediaId != mediaId))
        {
            return Result.Failure(MediaErrors.CollectionItemNotFound);
        }

        var reordered = new List<MediaId> { mediaId };

        reordered.AddRange(_items
            .OrderBy(item => item.Position)
            .Where(item => item.MediaId != mediaId)
            .Select(item => item.MediaId));

        return Reorder(reordered);
    }

    public Result Rename(string? name)
    {
        Name = Truncate(name, MaxNameLength);
        return Result.Success();
    }

    public bool Contains(MediaId mediaId) => _items.Any(item => item.MediaId == mediaId);

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

    private void Resequence()
    {
        var position = 0;

        foreach (var item in _items.OrderBy(candidate => candidate.Position))
        {
            item.MoveTo(position++);
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
