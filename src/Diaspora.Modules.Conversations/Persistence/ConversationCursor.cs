using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Conversations.Persistence;

/// <summary>
/// An opaque position in an ordered page.
/// <para>
/// <b>A timestamp and a boundary key, and the boundary key is the half that makes it correct.</b> A
/// cursor carrying only "everything before 14:02:31.447" silently drops every row sharing the
/// boundary instant with the last row returned — and in a conversation that is not an edge case, it
/// is what happens every time somebody sends a photograph and its caption together, or whenever two
/// rows are stamped inside one clock tick by the shared audit interceptor.
/// </para>
/// <para>
/// <b>The textbook keyset predicate cannot be written here</b> — identifiers are value-converted
/// strongly typed ids and no provider translates an ordering comparison on one, which is exactly
/// what the platform analyser refuses at build time. So the boundary key carries the identifiers
/// already returned at the boundary sort key and the query excludes them by set membership, which
/// translates everywhere. <see cref="Diaspora.Persistence.Paging.CursorBoundary"/> owns that
/// format, its accumulation rule and its overflow behaviour; this record only carries it.
/// </para>
/// <para>
/// Base64 rather than readable, because a caller that can read a cursor is a caller that will
/// eventually construct one.
/// </para>
/// </summary>
public sealed record ConversationCursor
{
    private const char Separator = '|';

    private ConversationCursor(long ticks, string boundaryKey)
    {
        Ticks = ticks;
        BoundaryKey = boundaryKey;
    }

    public long Ticks { get; }

    /// <summary>
    /// The identifiers already served at <see cref="Timestamp"/>, in
    /// <see cref="Diaspora.Persistence.Paging.CursorBoundary"/>'s format. Possibly its exhausted
    /// marker.
    /// </summary>
    public string BoundaryKey { get; }

    public DateTimeOffset Timestamp => new(Ticks, TimeSpan.Zero);

    public static ConversationCursor Create(DateTimeOffset timestamp, string boundaryKey) =>
        new(timestamp.UtcTicks, boundaryKey ?? string.Empty);

    /// <summary>
    /// Reads a cursor, or null when it is absent or unreadable.
    /// <para>
    /// An unreadable cursor is no cursor rather than an error: a client that kept one across an app
    /// upgrade gets the first page, which is recoverable, where a 400 mid-scroll is not.
    /// </para>
    /// </summary>
    public static ConversationCursor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = decoded.IndexOf(Separator, StringComparison.Ordinal);

            if (separator <= 0)
            {
                return null;
            }

            return long.TryParse(
                decoded[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
                ? new ConversationCursor(ticks, decoded[(separator + 1)..])
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    public string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{Ticks}{Separator}{BoundaryKey}")));
}
