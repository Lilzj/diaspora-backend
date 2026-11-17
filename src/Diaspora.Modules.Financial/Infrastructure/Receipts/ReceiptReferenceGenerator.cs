using System.Security.Cryptography;
using Diaspora.Modules.Financial.Extensibility.Receipts;

namespace Diaspora.Modules.Financial.Infrastructure.Receipts;

/// <summary>
/// Mints a receipt reference at the strength its receipt needs — BBL-004.
/// <para>
/// <c>DON-20260805-7KQ4M2</c> for a gift somebody owns, and a far longer suffix for one nobody does.
/// A prefix, the date, and characters from an alphabet with no I, O, 0 or 1 in it — because the one
/// thing the short form has to survive is being read aloud to a support agent.
/// </para>
/// <para>
/// <b>Which form is minted is decided by the caller, not here</b>, and
/// <see cref="ReceiptReferenceStrength"/> carries the reasoning: a receipt with an owner is served
/// to that owner, so its reference guards nothing; a receipt with no owner cannot be scoped to
/// anybody, so its reference is the entire access decision.
/// </para>
/// <para>
/// <b>Formerly <c>SequentialReceiptReferenceGenerator</c>, and the rename is the point.</b> The
/// suffix used to be carved out of a sequential GUID: <c>Guid.CreateVersion7().ToByteArray()</c>,
/// taking the last six bytes. Those bytes are a UUIDv7's <c>rand_b</c> and so were genuinely random,
/// but only because of two facts neither stated nor tested — that .NET leaves a GUID's final eight
/// bytes in order, and that 32 divides 256 exactly so <c>byte % 32</c> is unbiased. Both are true
/// today. A change to either, including something as innocent as adding a symbol to the alphabet,
/// would have quietly reduced the entropy rather than failing. There is no GUID here now, and no
/// modulo.
/// </para>
/// <para>
/// Uniqueness still comes from entropy rather than a counter, so no two instances coordinate; the
/// database's unique index is what makes it a guarantee rather than a probability. The date in the
/// reference means a collision has to land on the same day under the same prefix, which is why 30
/// bits is sound for the short form even at launch volumes while being hopeless as a secret.
/// </para>
/// </summary>
public sealed class ReceiptReferenceGenerator : IReceiptReferenceGenerator
{
    /// <summary>No I, O, 0 or 1. Everything that survives being read aloud.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>Six characters a person can dictate. Access to these receipts is by ownership.</summary>
    private const int HandleSuffixLength = 6;

    /// <summary>
    /// Twenty-six characters, which is 130 bits — the first multiple of five symbols that clears the
    /// 128-bit floor <see cref="IReceiptReferenceGenerator"/> states.
    /// <para>
    /// Nobody dictates this one and nobody needs to: it is held only by the client that made the
    /// gift, and the giver of an anonymous gift has no account for a support agent to look it up
    /// against in the first place.
    /// </para>
    /// </summary>
    private const int CredentialSuffixLength = 26;

    /// <summary>Bits per character: a 32-symbol alphabet drawn uniformly.</summary>
    public const int BitsPerCharacter = 5;

    /// <summary>
    /// The entropy of an unguessable reference, in bits. Public so a test can assert the floor with
    /// the arithmetic in front of it — shortening the suffix changes this number and fails that test
    /// rather than passing quietly, which is exactly how the original 30 bits came to be shipped.
    /// </summary>
    public const int CredentialBits = CredentialSuffixLength * BitsPerCharacter;

    public string Next(string prefix, DateTimeOffset now, ReceiptReferenceStrength strength)
    {
        var head = string.IsNullOrWhiteSpace(prefix) ? "REF" : prefix.Trim().ToUpperInvariant();

        var length = strength == ReceiptReferenceStrength.Credential
            ? CredentialSuffixLength
            : HandleSuffixLength;

        var suffix = new char[length];

        for (var index = 0; index < length; index++)
        {
            // GetInt32 rather than a byte and a modulo. It is unbiased by construction for any
            // alphabet length, where the modulo was unbiased only for lengths that divide 256 — a
            // property of this particular alphabet that nothing anywhere asserted.
            suffix[index] = Alphabet[RandomNumberGenerator.GetInt32(0, Alphabet.Length)];
        }

        return $"{head}-{now.UtcDateTime:yyyyMMdd}-{new string(suffix)}";
    }
}
