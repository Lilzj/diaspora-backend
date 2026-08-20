namespace Diaspora.Persistence.Atomic;

/// <summary>
/// What the database did with a guarded update.
/// <para>
/// <b>There are only two answers, and the ambiguity in the second one is deliberate.</b> A statement
/// that changed one row applied; a statement that changed none did not, and the reason is either
/// that the guard refused or that the row is not there. Distinguishing them would cost a second
/// query on every failure, and no caller needs it: a caller that must tell the difference has
/// already loaded the row, because it needs the aggregate for everything else it is about to do.
/// </para>
/// </summary>
public sealed class AtomicUpdateResult
{
    private static readonly IReadOnlyDictionary<string, long> Nothing =
        new Dictionary<string, long>(StringComparer.Ordinal);

    internal AtomicUpdateResult(int rowsAffected, IReadOnlyDictionary<string, long>? state = null)
    {
        RowsAffected = rowsAffected;
        State = state ?? Nothing;
    }

    /// <summary>Whether the guard held and the row changed.</summary>
    public bool Applied => RowsAffected > 0;

    public int RowsAffected { get; }

    /// <summary>
    /// The post-statement values of the columns the caller asked to read back, keyed by the same
    /// property paths it named. Empty when nothing was requested or nothing applied.
    /// </summary>
    public IReadOnlyDictionary<string, long> State { get; }

    /// <summary>The read-back value of one path, or null when it was not requested.</summary>
    public long? Value(string path) =>
        State.TryGetValue(path, out var value) ? value : null;
}
