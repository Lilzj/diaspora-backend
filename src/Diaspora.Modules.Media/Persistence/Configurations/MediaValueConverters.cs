using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Media.Persistence.Configurations;

/// <summary>
/// The value converters every Media configuration shares.
/// <para>
/// Declared once rather than inline at each use site, because the same value object appears on
/// several tables and an inline converter that differed in one of them — a different trim, a
/// different case fold — would produce rows that no longer join. The storage form of a value
/// object is a property of the value object.
/// </para>
/// <para>
/// Exposed as the non-generic <see cref="ValueConverter"/> so one converter serves both the
/// nullable and non-nullable positions: EF treats
/// <c>ValueConverter&lt;ContentHash, string&gt;</c> and
/// <c>ValueConverter&lt;ContentHash?, string&gt;</c> as different types, and two converters
/// that can drift is worse than one used in both places. This is the same reasoning the
/// Location module records for its own converters.
/// </para>
/// <para>
/// Every converter parses rather than tries. A value that was valid when written must
/// round-trip, and a row that will not materialise is a defect worth failing loudly on rather
/// than silently coercing.
/// </para>
/// </summary>
internal static class MediaValueConverters
{
    public static ValueConverter ContentType { get; } =
        new ValueConverter<MediaContentType, string>(
            type => type.Value, value => MediaContentType.Create(value));

    public static ValueConverter ContentHash { get; } =
        new ValueConverter<ContentHash, string>(
            hash => hash.Value, value => Domain.ValueObjects.ContentHash.Create(value));

    public static ValueConverter StorageKey { get; } =
        new ValueConverter<StorageKey, string>(
            key => key.Value, value => Domain.ValueObjects.StorageKey.Create(value));

    /// <summary>
    /// Stores the <em>original</em> name and reconstructs the sanitised one.
    /// <para>
    /// This direction, and not the other, for a reason worth stating. Sanitisation is
    /// deterministic and lossy: the original cannot be recovered from the sanitised form, but
    /// the sanitised form is always recoverable from the original. Storing the original means
    /// one column instead of two, and it means a change to the sanitiser applies to every
    /// existing row rather than leaving historical names sanitised by an older rule.
    /// </para>
    /// </summary>
    public static ValueConverter FileName { get; } =
        new ValueConverter<SafeFileName, string>(
            name => name.Original, value => SafeFileName.Create(value));

    public static ValueConverter ByteSize { get; } =
        new ValueConverter<ByteSize, long>(
            size => size.Bytes, value => Domain.ValueObjects.ByteSize.FromBytes(value));

    /// <summary>A strongly typed identifier converter, in the non-generic form.</summary>
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
