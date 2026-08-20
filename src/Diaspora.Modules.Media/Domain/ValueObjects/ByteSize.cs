using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// A non-negative count of bytes.
/// <para>
/// A type rather than a <c>long</c> because the codebase would otherwise carry three units at
/// once — bytes, kilobytes and megabytes — in identically-shaped parameters. The B3 review of
/// Trust's document limit found exactly that kind of ambiguity in a comment; here the unit is
/// in the type and the megabyte helper is a factory, so <c>ByteSize.FromMegabytes(5)</c> is
/// unambiguous at the call site.
/// </para>
/// </summary>
public sealed class ByteSize : ValueObject, IComparable<ByteSize>
{
    public const long BytesPerKilobyte = 1024;

    public const long BytesPerMegabyte = 1024 * BytesPerKilobyte;

    public static readonly ByteSize Zero = new(0);

    private ByteSize(long bytes)
    {
        Bytes = bytes;
    }

    public long Bytes { get; }

    public double Megabytes => (double)Bytes / BytesPerMegabyte;

    public static ByteSize FromBytes(long bytes) =>
        bytes < 0
            ? throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "A size cannot be negative.")
            : new ByteSize(bytes);

    public static ByteSize FromKilobytes(long kilobytes) => FromBytes(kilobytes * BytesPerKilobyte);

    public static ByteSize FromMegabytes(long megabytes) => FromBytes(megabytes * BytesPerMegabyte);

    public static Result<ByteSize> TryFromBytes(long bytes) =>
        bytes < 0
            ? Result.Failure<ByteSize>(Error.Validation(
                MediaErrorCodes.UploadSizeMismatch, "A size cannot be negative."))
            : Result.Success(new ByteSize(bytes));

    public bool Exceeds(ByteSize other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Bytes > other.Bytes;
    }

    public ByteSize Add(ByteSize other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new ByteSize(Bytes + other.Bytes);
    }

    public int CompareTo(ByteSize? other) => other is null ? 1 : Bytes.CompareTo(other.Bytes);

    /// <summary>
    /// Declared explicitly rather than inherited from <see cref="ValueObject"/> because a type
    /// that orders must also compare: <c>a &lt;= b</c> and <c>a == b</c> resolving through
    /// different implementations is how a comparison chain ends up inconsistent with itself.
    /// </summary>
    public static bool operator ==(ByteSize? left, ByteSize? right) => Equals(left, right);

    public static bool operator !=(ByteSize? left, ByteSize? right) => !Equals(left, right);

    public override bool Equals(object? obj) => obj is ByteSize other && Bytes == other.Bytes;

    public override int GetHashCode() => Bytes.GetHashCode();

    public static bool operator <(ByteSize? left, ByteSize? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(ByteSize? left, ByteSize? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(ByteSize? left, ByteSize? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(ByteSize? left, ByteSize? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    /// <summary>
    /// A human-readable form, for error messages a member will read. "That file is 18.4 MB"
    /// is a sentence; "that file is 19293798 bytes" is not.
    /// </summary>
    public override string ToString() => Bytes switch
    {
        < BytesPerKilobyte => $"{Bytes} B",
        < BytesPerMegabyte => $"{Bytes / (double)BytesPerKilobyte:0.#} KB",
        _ => $"{Megabytes:0.#} MB"
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bytes;
    }
}
