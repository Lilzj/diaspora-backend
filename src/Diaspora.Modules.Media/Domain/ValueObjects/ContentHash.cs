using System.Globalization;
using System.Security.Cryptography;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// A SHA-256 digest of an asset's bytes, lower-case hex.
/// <para>
/// Three jobs, and it is worth being explicit that they are three rather than one.
/// </para>
/// <list type="number">
/// <item>
/// <b>Integrity.</b> A chunked upload that reassembles to a different digest than the client
/// declared did not arrive intact, and completing it would store a corrupt file that looks
/// fine until somebody opens it.
/// </item>
/// <item>
/// <b>Deduplication.</b> Two members uploading the same listing photo produce one stored
/// object. At the scale of a gallery-heavy marketplace this is not a micro-optimisation.
/// </item>
/// <item>
/// <b>Reuse detection.</b> The client's <c>TrustEngine.detectImageReuse</c> fingerprints by
/// URL string, which any re-upload defeats. Publishing a content hash lets Trust enforce the
/// same rule against the bytes. Media computes the hash and never decides what reuse means —
/// that judgement belongs to the module that owns fraud.
/// </item>
/// </list>
/// <para>
/// SHA-256 rather than a faster non-cryptographic hash precisely because of the third job: a
/// fingerprint an adversary can collide on is a fraud check an adversary can defeat.
/// </para>
/// </summary>
public sealed class ContentHash : ValueObject
{
    /// <summary>32 bytes as hex.</summary>
    public const int Length = 64;

    public const string Algorithm = "SHA-256";

    private ContentHash(string value)
    {
        Value = value;
    }

    /// <summary>The digest, lower-case hex, no prefix.</summary>
    public string Value { get; }

    /// <summary>
    /// The first four bytes, for log lines and error messages. Enough to correlate two events,
    /// far too little to confirm a guess about the content.
    /// </summary>
    public string Abbreviated => Value[..8];

    public static ContentHash Compute(ReadOnlySpan<byte> content) =>
        new(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());

    public static async Task<ContentHash> ComputeAsync(
        Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var digest = await SHA256.HashDataAsync(content, cancellationToken);
        return new ContentHash(Convert.ToHexString(digest).ToLowerInvariant());
    }

    public static Result<ContentHash> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<ContentHash>(Error.Validation(
                MediaErrorCodes.UploadChunkChecksumMismatch, "A content hash is required."));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length != Length || !normalized.All(char.IsAsciiHexDigitLower))
        {
            return Result.Failure<ContentHash>(Error.Validation(
                MediaErrorCodes.UploadChunkChecksumMismatch,
                $"A content hash must be {Length} lower-case hexadecimal characters."));
        }

        return Result.Success(new ContentHash(normalized));
    }

    public static ContentHash Create(string value) =>
        TryCreate(value) is { IsSuccess: true } result
            ? result.Value
            : throw new ArgumentException("Not a SHA-256 digest.", nameof(value));

    /// <summary>
    /// Constant-time comparison. A checksum check is a place where an early-exit comparison
    /// leaks how much of a guess was right, and the cost of not leaking it is nothing.
    /// </summary>
    public bool Matches(ContentHash other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Value), Convert.FromHexString(other.Value));
    }

    /// <summary>
    /// A weak entity tag derived from the digest, for <c>If-None-Match</c>. Quoted, because an
    /// unquoted ETag is not an ETag.
    /// </summary>
    public string ToEntityTag() =>
        string.Create(CultureInfo.InvariantCulture, $"\"{Value[..32]}\"");

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
