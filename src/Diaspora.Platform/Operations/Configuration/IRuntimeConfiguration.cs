namespace Diaspora.Platform.Operations.Configuration;

/// <summary>
/// Reads runtime settings.
/// <para>
/// Every accessor is total: an unknown key, an unparseable value or a value that fails validation all
/// return the declared default rather than throwing. <b>A configuration read is not a place a request
/// should die.</b> The failure is recorded — a metric, a log line and a dashboard entry — so that a
/// bad value is loud without being fatal.
/// </para>
/// </summary>
public interface IRuntimeConfiguration
{
    string GetString(string key);

    int GetInt32(string key);

    double GetDouble(string key);

    bool GetBoolean(string key);

    TimeSpan GetDuration(string key);

    IReadOnlyList<string> GetList(string key);

    /// <summary>Every declared setting and its current value. For the operational dashboard.</summary>
    IReadOnlyList<ConfigurationEntry> All { get; }

    /// <summary>One setting's current state, or null when the key is not declared.</summary>
    ConfigurationEntry? Find(string key);

    /// <summary>
    /// Raised after a value changes, so a component holding a derived object can rebuild it.
    /// <para>
    /// The hot-reload seam. Most consumers need nothing — they call a getter per use and see the new
    /// value — but a component that compiled a policy or opened a connection pool from a setting has
    /// to be told.
    /// </para>
    /// </summary>
    event EventHandler<ConfigurationChange>? Changed;
}

/// <summary>
/// Changes runtime settings. Separate from reading, because reading is on request paths and changing
/// is a staff operation behind a permission.
/// </summary>
public interface IRuntimeConfigurationAdministration
{
    /// <summary>
    /// Sets a value.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The key is not declared by any module.</exception>
    /// <exception cref="ArgumentException">The value fails the declaration or a validator.</exception>
    Task SetAsync(
        string key, string value, string? changedBy, string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a setting to its declared default.</summary>
    Task ResetAsync(string key, string? changedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a value without applying it.
    /// <para>
    /// What the administration surface calls before showing a confirmation. An operator changing a
    /// production setting should learn that the value is wrong before the change, not from the
    /// dashboard afterwards.
    /// </para>
    /// </summary>
    ConfigurationValidation Validate(string key, string value);

    /// <summary>The change history, newest first, with sensitive values redacted.</summary>
    Task<IReadOnlyList<ConfigurationChange>> HistoryAsync(
        string? key = null, int limit = 100, CancellationToken cancellationToken = default);
}
