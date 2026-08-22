namespace Diaspora.Platform.Operations.Privacy;

/// <summary>
/// A member's export, assembled across every module that can contribute one.
/// </summary>
/// <param name="UserId">Who it is about.</param>
/// <param name="GeneratedAt">When it was assembled. An export is a snapshot and says so.</param>
/// <param name="Sections">What each module contributed.</param>
/// <param name="ModulesWithoutExportProvider">
/// The modules that hold data and have no <see cref="IDataExportProvider"/>.
/// <para>
/// <b>Carried in the export itself rather than left out of it.</b> An export that silently omitted
/// half the platform would be worse than no export: the member would reasonably read it as "this is
/// everything Diaspora holds about me". This names the modules whose contribution is missing, so the
/// document is honest about its own completeness.
/// </para>
/// </param>
public sealed record PersonalDataExport(
    Guid UserId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ExportSection> Sections,
    IReadOnlyList<string> ModulesWithoutExportProvider);

/// <summary>
/// What an erasure did, or would do.
/// </summary>
/// <param name="UserId">Who.</param>
/// <param name="CompletedAt">When. Null on a preview, which changes nothing.</param>
/// <param name="IsPreview">Whether anything actually happened.</param>
/// <param name="Results">What each module reported.</param>
/// <param name="ModulesWithoutErasureProvider">
/// The modules with no <see cref="IDataErasureProvider"/> — that is, the ones that could not be asked.
/// Named for the same reason the export names its gaps: a receipt that listed only what was erased
/// would read as a complete one.
/// </param>
/// <param name="Failures">
/// Modules whose erasure threw. Recorded rather than swallowed, because a partial erasure that
/// reported success is the worst outcome available here.
/// </param>
public sealed record AccountErasureReceipt(
    Guid UserId,
    DateTimeOffset? CompletedAt,
    bool IsPreview,
    IReadOnlyList<ErasureResult> Results,
    IReadOnlyList<string> ModulesWithoutErasureProvider,
    IReadOnlyList<string> Failures)
{
    /// <summary>Whether every module that could be asked answered without throwing.</summary>
    public bool IsComplete => Failures.Count == 0;
}

/// <summary>
/// Assembles a member's data from every module that can produce it.
/// </summary>
public interface IPersonalDataExportService
{
    Task<PersonalDataExport> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs an erasure across every module that can perform one.
/// <para>
/// <b>Ordering is deliberate and Identity goes last.</b> Every other provider needs to be able to find
/// the member while it works; anonymising the identity first would leave the modules that came after
/// it erasing on behalf of somebody the platform can no longer name.
/// </para>
/// <para>
/// <b>One module's failure does not abandon the rest.</b> The alternative — stop at the first
/// exception — leaves a member half-erased with no record of where it stopped. Each module is asked,
/// each answer is recorded, and the receipt says which ones threw so the request can be finished
/// rather than guessed at.
/// </para>
/// </summary>
public interface IAccountErasureService
{
    /// <summary>What would happen. Changes nothing.</summary>
    Task<AccountErasureReceipt> PreviewAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Performs the erasure. Irreversible.</summary>
    Task<AccountErasureReceipt> EraseAsync(Guid userId, CancellationToken cancellationToken = default);
}
