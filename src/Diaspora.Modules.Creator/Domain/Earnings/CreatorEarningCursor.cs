using System.Globalization;

namespace Diaspora.Modules.Creator.Domain.Earnings;

/// <summary>
/// A position in one creator's ledger.
/// <para>
/// <b>Two fields, because a timestamp alone loses rows.</b> The obvious cursor is "everything older
/// than the last row I saw", and it is wrong the moment two earnings share an instant — which is
/// ordinary here, since one purchase settling against two subjects records both at the same
/// <c>OccurredAt</c>. A strict comparison on the timestamp drops the second one; a non-strict one
/// returns the first for ever. Either way a creator is looking at a ledger with a hole in it and no
/// way to know.
/// </para>
/// <para>
/// <b>Opaque on the wire.</b> It is serialised as one string and parsed back here, so a client
/// cannot assemble a cursor of its own — the only valid cursor is one this platform issued. A
/// malformed one is refused rather than ignored, because ignoring it silently serves page one
/// again and a "load more" button that reloads the same rows looks like an empty ledger.
/// </para>
/// </summary>
/// <param name="OccurredAt">When the last row on the previous page occurred.</param>
/// <param name="Id">That row's identity, breaking a tie on the instant.</param>
public readonly record struct CreatorEarningCursor(DateTimeOffset OccurredAt, Guid Id)
{
    private const char Separator = '|';

    /// <summary>Round-trips exactly: `O` keeps the offset and every tick.</summary>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{OccurredAt:O}{Separator}{Id:N}");

    public static bool TryParse(string? value, out CreatorEarningCursor cursor)
    {
        cursor = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(Separator);

        if (parts.Length != 2
            || !DateTimeOffset.TryParse(
                parts[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var occurredAt)
            || !Guid.TryParse(parts[1], out var id))
        {
            return false;
        }

        cursor = new CreatorEarningCursor(occurredAt, id);
        return true;
    }
}
