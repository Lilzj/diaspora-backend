using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// A pointer to any addressable piece of content on the platform.
/// <para>
/// <b>The type that makes B6 reusable.</b> Every social record — comment, reaction, report,
/// bookmark, share, mention — stores one of these instead of a foreign key to a post. That is
/// the whole difference between a community module and a social platform: a foreign key would
/// mean Guides needs its own comment table, and a reference means Guides needs nothing.
/// </para>
/// <para>
/// The pair is stored as two columns rather than a composite string, because every query that
/// matters filters on the type and the identifier together, and a composite string cannot be
/// range-scanned by type. The brief names this concept CommunityReference; the shorter name is
/// used here because the type appears in nearly every signature in the module.
/// </para>
/// </summary>
public sealed class ContentReference : ValueObject
{
    private ContentReference(CommunityContentType type, Guid id)
    {
        Type = type;
        Id = id;
    }

    private ContentReference() { }

    public CommunityContentType Type { get; private set; }

    public Guid Id { get; private set; }

    /// <summary>
    /// A stable, sortable, single-column form. Used only where a unique index has to span the
    /// reference and something else, which is the same problem B5 solved with a denormalised
    /// owner key: an index cannot span an owned type columns and the aggregate own.
    /// </summary>
    public string Key => $"{(int)Type}:{Id:N}";

    public static ContentReference Create(CommunityContentType type, Guid id) => new(type, id);

    public static ContentReference To(CommunityContentType type, StronglyTypedId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new ContentReference(type, id.Value);
    }

    public override string ToString() => Key;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Id;
    }
}
