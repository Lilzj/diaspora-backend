using Diaspora.Platform.Operations.Recovery;

namespace Diaspora.Operations.Recovery;

/// <summary>
/// The backup provider that ships: none, said out loud.
/// <para>
/// <b>No backup engine is written here, and none should be.</b> Every managed database takes backups
/// on a schedule this platform does not control and could not improve on; a second, worse engine
/// beside it would be the wrong deliverable. What was actually missing is that nothing in the platform
/// could say whether backups exist, when the last one was taken, or whether one has ever been restored.
/// </para>
/// <para>
/// Registering this rather than nothing is the point. <c>ListAsync</c> returning empty and
/// <c>Name</c> returning <c>none</c> makes "no backup provider is configured" a state the dashboard
/// renders as <see cref="Platform.Operations.Dashboards.DashboardStatus.Unknown"/> and the readiness
/// report can quote — instead of a section that silently does not appear.
/// </para>
/// </summary>
public sealed class UnconfiguredBackupProvider : IBackupProvider
{
    public string Name => "none";

    public Task<IReadOnlyList<BackupDescriptor>> ListAsync(
        int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackupDescriptor>>([]);

    public Task<BackupDescriptor?> LatestAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<BackupDescriptor?>(null);
}
