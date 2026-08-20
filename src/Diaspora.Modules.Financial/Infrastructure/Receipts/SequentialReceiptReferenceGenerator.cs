using Diaspora.Modules.Financial.Extensibility.Receipts;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Infrastructure.Receipts;

/// <summary>
/// Produces a receipt reference a person can read over a telephone.
/// <para>
/// <c>DON-20260805-7KQ4M2</c>. A prefix, the date, and six characters from an alphabet with no I, O,
/// 0 or 1 in it — because the one thing this string has to survive is somebody reading it aloud to
/// a support agent. <b>It is the only key an anonymous donor has</b>, their identifier having
/// deliberately not been stored, so uniqueness and legibility are both requirements. A GUID
/// satisfies the first and fails the second badly.
/// </para>
/// <para>
/// Uniqueness comes from a sequential GUID's entropy rather than from a counter, so no two instances
/// coordinate; the database's unique index is what makes it a guarantee rather than a probability.
/// </para>
/// </summary>
public sealed class SequentialReceiptReferenceGenerator : IReceiptReferenceGenerator
{
    /// <summary>No I, O, 0 or 1. Everything that survives being read aloud.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private const int SuffixLength = 6;

    private readonly IGuidGenerator _guids;

    public SequentialReceiptReferenceGenerator(IGuidGenerator guids) => _guids = guids;

    public string Next(string prefix, DateTimeOffset now)
    {
        var head = string.IsNullOrWhiteSpace(prefix) ? "REF" : prefix.Trim().ToUpperInvariant();
        var bytes = _guids.NewSequentialGuid().ToByteArray();
        var suffix = new char[SuffixLength];

        for (var index = 0; index < SuffixLength; index++)
        {
            suffix[index] = Alphabet[bytes[^(index + 1)] % Alphabet.Length];
        }

        return $"{head}-{now.UtcDateTime:yyyyMMdd}-{new string(suffix)}";
    }
}
