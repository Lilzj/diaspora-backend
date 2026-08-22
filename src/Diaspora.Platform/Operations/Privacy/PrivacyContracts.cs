namespace Diaspora.Platform.Operations.Privacy;

/// <summary>What a module did with a member's data when asked to erase it.</summary>
public enum ErasureOutcome
{
    /// <summary>The rows were deleted.</summary>
    Deleted = 0,

    /// <summary>
    /// Identifying fields were replaced and the record kept.
    /// <para>
    /// The right answer far more often than deletion. A ledger entry cannot be deleted without
    /// destroying the balance derived from it, and a comment cannot be deleted without breaking the
    /// thread it belongs to — but neither needs to carry a name.
    /// </para>
    /// </summary>
    Anonymized = 1,

    /// <summary>Nothing of this member's was held.</summary>
    NothingHeld = 2,

    /// <summary>
    /// Kept, because something outside this platform's discretion requires it.
    /// <para>
    /// Financial records under a statutory retention period are the case that exists. The module says
    /// so explicitly and gives the reason, so the export handed to the member can say what was kept and
    /// why rather than quietly omitting it.
    /// </para>
    /// </summary>
    Retained = 3
}

/// <summary>
/// What one module did.
/// <para>
/// <b>The outcome serialises as its name.</b> The host does not register a global string-enum
/// converter — every one of the eight hundred and eighty-nine published operations would change
/// shape if it did — so this one property carries its own. A member's deletion receipt is read by a
/// person, and <c>"outcome": 3</c> is not something a client should have to hold a lookup table for.
/// </para>
/// </summary>
/// <param name="Module">Who.</param>
/// <param name="DataSet">What.</param>
/// <param name="Outcome">Which of the four.</param>
/// <param name="RecordsAffected">How many.</param>
/// <param name="Reason">Required when the outcome is <see cref="ErasureOutcome.Retained"/>.</param>
public sealed record ErasureResult(
    string Module,
    string DataSet,
    [property: System.Text.Json.Serialization.JsonConverter(
        typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    ErasureOutcome Outcome,
    int RecordsAffected,
    string? Reason = null);

/// <summary>
/// Erases or anonymizes a member's data.
/// <para>
/// <b>One implementation per module, and the platform never guesses.</b> A central eraser that walked
/// the schema deleting rows containing a user id would break every foreign key on the platform and
/// destroy the ledger. Only the module that owns a table knows whether its rows should be deleted,
/// anonymized or kept, and what has to happen to the things pointing at them.
/// </para>
/// <para>
/// <b>Erasure does not reach backups</b>, and that is the honest position rather than an oversight: a
/// backup is immutable by design, and a system that could rewrite one has a worse problem than the one
/// being solved. The answer is retention — the backup ages out — which is why <c>RecoveryPolicy</c>
/// carries both a retention period and a backup retention, and why the privacy audit reads both.
/// </para>
/// </summary>
public interface IDataErasureProvider
{
    /// <summary>The owning module.</summary>
    string Module { get; }

    /// <summary>
    /// What this module holds about a member, without changing anything.
    /// <para>
    /// Run first, so a member can be told what will happen before it does — and so the operation can be
    /// reviewed before it is irreversible.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ErasureResult>> PreviewAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Performs the erasure.</summary>
    Task<IReadOnlyList<ErasureResult>> EraseAsync(
        Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>One module's contribution to a data export.</summary>
/// <param name="Module">Who.</param>
/// <param name="DataSet">What.</param>
/// <param name="Records">
/// The records, as flat dictionaries. Weakly typed because the export is assembled across every module
/// on the platform and a typed contract would need this assembly to know all of them.
/// </param>
public sealed record ExportSection(
    string Module,
    string DataSet,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Records);

/// <summary>
/// Produces a member's data for export.
/// <para>
/// <b>What a member is entitled to is what they contributed, not what the platform derived.</b> A
/// module exports the posts, applications and messages a member wrote; it does not export their
/// affinity vector or their risk score, which are the platform's opinions rather than the member's
/// data. That line is drawn per module because only the module can draw it.
/// </para>
/// </summary>
public interface IDataExportProvider
{
    string Module { get; }

    Task<IReadOnlyList<ExportSection>> ExportAsync(
        Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>A consent decision.</summary>
/// <param name="UserId">Who.</param>
/// <param name="Purpose">What for — <c>analytics</c>, <c>marketing</c>, <c>personalization</c>.</param>
/// <param name="Granted">Whether it was given.</param>
/// <param name="RecordedAt">When.</param>
/// <param name="Version">
/// Which version of the notice they were shown. Without it, a consent record cannot answer the only
/// question that matters later — what were they actually agreeing to.
/// </param>
public sealed record ConsentRecord(
    Guid UserId,
    string Purpose,
    bool Granted,
    DateTimeOffset RecordedAt,
    string Version);

/// <summary>
/// Records and reads consent.
/// <para>
/// <b>A hook, as the brief specifies, and not a consent engine.</b> The platform has no consent
/// surface — no notice, no version, no place a member is asked — so an implementation here would be
/// inventing a product decision. What ships is the contract the eventual surface writes through, and a
/// recorder that stores what it is given, so that a module wanting to check consent before doing
/// something has something to call from the start.
/// </para>
/// </summary>
public interface IConsentRecorder
{
    Task RecordAsync(ConsentRecord consent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether consent is currently held. <b>Absence is not consent</b> — an unrecorded purpose returns
    /// false, which is the only defensible default.
    /// </summary>
    Task<bool> HasConsentAsync(Guid userId, string purpose, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConsentRecord>> HistoryAsync(
        Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>What the privacy audit found for one data set.</summary>
/// <param name="Module">Who owns it.</param>
/// <param name="DataSet">What it is.</param>
/// <param name="PolicyName">The recovery policy that governs its retention.</param>
/// <param name="RetentionPeriod">How long it is kept, or null when nothing says.</param>
/// <param name="HasErasureProvider">Whether the module can erase it on request.</param>
/// <param name="HasExportProvider">Whether the module can export it on request.</param>
/// <param name="HasRetentionSweeper">Whether anything actually enforces the retention period.</param>
/// <param name="Findings">What is missing.</param>
public sealed record PrivacyAuditEntry(
    string Module,
    string DataSet,
    string? PolicyName,
    TimeSpan? RetentionPeriod,
    bool HasErasureProvider,
    bool HasExportProvider,
    bool HasRetentionSweeper,
    IReadOnlyList<string> Findings);

/// <summary>
/// Checks that every module's data has an owner, a retention period, and a way to be erased and
/// exported.
/// <para>
/// The point is the gaps, not the coverage. A module with personal data and no erasure provider is a
/// module that cannot honour a deletion request, and until something enumerates the modules and asks,
/// nobody finds out until a request arrives.
/// </para>
/// </summary>
public interface IPrivacyAuditor
{
    Task<IReadOnlyList<PrivacyAuditEntry>> AuditAsync(CancellationToken cancellationToken = default);
}
