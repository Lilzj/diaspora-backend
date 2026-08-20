using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// What an asset was uploaded for: a type and the identifier of the thing.
/// <para>
/// This is the seam that keeps Media ignorant of every vertical. Media stores that an asset
/// belongs to <c>HousingListing 7f3c…</c> and knows nothing else about housing listings — not
/// their table, not their rules, not that the module exists. When Housing wants to know
/// whether a caller may see the photo, it answers through <c>IMediaAccessPolicy</c>; Media
/// asks, it does not decide.
/// </para>
/// <para>
/// The identifier is a <see cref="Guid"/> and not a string. Every aggregate on this platform
/// is keyed by one, and a string would invite composite keys, slugs and stringly-typed lookups
/// into an index that has to stay narrow.
/// </para>
/// </summary>
public sealed class MediaOwner : ValueObject
{
    /// <summary>An upload nothing has claimed yet. The state every direct upload starts in.</summary>
    public static readonly MediaOwner Unassigned = new(MediaOwnerType.Unassigned, Guid.Empty);

    private MediaOwner(MediaOwnerType type, Guid id)
    {
        Type = type;
        Id = id;
    }

    public MediaOwnerType Type { get; }

    /// <summary>The owning entity's identifier. <see cref="Guid.Empty"/> when unassigned.</summary>
    public Guid Id { get; }

    public bool IsAssigned => Type != MediaOwnerType.Unassigned && Id != Guid.Empty;

    public static Result<MediaOwner> TryCreate(MediaOwnerType type, Guid id)
    {
        if (type == MediaOwnerType.Unassigned)
        {
            return id == Guid.Empty
                ? Result.Success(Unassigned)
                : Result.Failure<MediaOwner>(Error.Validation(
                    MediaErrorCodes.ReferenceOwnerInvalid,
                    "An unassigned owner cannot carry an identifier."));
        }

        if (id == Guid.Empty)
        {
            return Result.Failure<MediaOwner>(Error.Validation(
                MediaErrorCodes.ReferenceOwnerInvalid,
                "An owner type requires the identifier of the thing that owns the asset."));
        }

        return Result.Success(new MediaOwner(type, id));
    }

    public static MediaOwner Create(MediaOwnerType type, Guid id) =>
        TryCreate(type, id) is { IsSuccess: true } result
            ? result.Value
            : throw new ArgumentException("Not a usable media owner.", nameof(type));

    public override string ToString() => IsAssigned ? $"{Type}:{Id:N}" : "unassigned";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Id;
    }
}
