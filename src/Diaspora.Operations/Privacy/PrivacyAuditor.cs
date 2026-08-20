using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.Platform.Operations.Recovery;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Operations.Privacy;

/// <summary>
/// Walks the module registry and asks, of each module, whether its data can be exported, erased and
/// aged out.
/// <para>
/// <b>The findings are the deliverable.</b> A privacy audit that reported only what is covered would be
/// a document about the modules that already do the right thing. This enumerates every registered
/// module and reports the ones with no erasure provider, no export provider, no retention sweeper or no
/// retention period — which is the list somebody needs before a deletion request arrives rather than
/// after.
/// </para>
/// <para>
/// It cannot know whether a module holds personal data; nothing can, without a classification the
/// platform has never carried. So it reports absence as a finding rather than as a failure, and the
/// module that legitimately holds nothing closes its finding by saying so in the guide. That is a
/// weaker check than a schema-level classification and it is the honest one available today.
/// </para>
/// </summary>
public sealed class PrivacyAuditor : IPrivacyAuditor
{
    private readonly ModuleRegistry _modules;
    private readonly IServiceScopeFactory _scopes;
    private readonly IOperationalPolicyCatalog _policies;

    /// <summary>
    /// Resolves the providers per audit rather than holding them, for the same reason the health
    /// aggregator does: an erasure or export provider reaches into its module's data and is therefore
    /// scoped, and a singleton that captured one would hold a disposed <c>DbContext</c>.
    /// </summary>
    public PrivacyAuditor(
        ModuleRegistry modules,
        IServiceScopeFactory scopes,
        IOperationalPolicyCatalog policies)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(policies);

        _modules = modules;
        _scopes = scopes;
        _policies = policies;
    }

    public Task<IReadOnlyList<PrivacyAuditEntry>> AuditAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<PrivacyAuditEntry>();

        using var scope = _scopes.CreateScope();

        var erasers = scope.ServiceProvider.GetServices<IDataErasureProvider>().ToArray();
        var exporters = scope.ServiceProvider.GetServices<IDataExportProvider>().ToArray();
        var registered = scope.ServiceProvider.GetServices<IRetentionSweeper>().ToArray();

        foreach (var module in _modules.Modules.OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            var hasEraser = erasers.Any(eraser => Same(eraser.Module, module.Name));
            var hasExporter = exporters.Any(exporter => Same(exporter.Module, module.Name));

            var sweepers = registered.Where(sweeper => Same(sweeper.Module, module.Name)).ToArray();

            if (sweepers.Length == 0)
            {
                entries.Add(Build(module.Name, dataSet: "(module)", policy: null, hasEraser, hasExporter,
                    hasSweeper: false));

                continue;
            }

            // A module with several sweepers gets one entry per data set. The unit of the audit is the
            // data set rather than the module, because a module that can erase its comments and not its
            // attachments is not covered and a module-level answer would say it was.
            foreach (var sweeper in sweepers)
            {
                entries.Add(Build(
                    module.Name,
                    sweeper.DataSet,
                    _policies.Find<RecoveryPolicy>(sweeper.PolicyName),
                    hasEraser,
                    hasExporter,
                    hasSweeper: true));
            }
        }

        return Task.FromResult<IReadOnlyList<PrivacyAuditEntry>>(entries);
    }

    private static PrivacyAuditEntry Build(
        string module,
        string dataSet,
        RecoveryPolicy? policy,
        bool hasEraser,
        bool hasExporter,
        bool hasSweeper)
    {
        var findings = new List<string>();

        if (!hasEraser)
        {
            findings.Add(
                "No IDataErasureProvider. If this module holds anything about a member, it cannot " +
                "honour a deletion request.");
        }

        if (!hasExporter)
        {
            findings.Add(
                "No IDataExportProvider. Anything a member contributed here is absent from their export.");
        }

        if (!hasSweeper)
        {
            findings.Add(
                "No IRetentionSweeper. Whatever this module holds is held indefinitely.");
        }
        else if (policy is null)
        {
            findings.Add(
                "The sweeper names a recovery policy the catalogue does not have, so no retention " +
                "period governs it.");
        }
        else if (policy.RetentionPeriod is null)
        {
            findings.Add(
                $"'{policy.Name}' declares no retention period, so the sweeper has no cutoff to apply.");
        }

        return new PrivacyAuditEntry(
            module,
            dataSet,
            policy?.Name,
            policy?.RetentionPeriod,
            hasEraser,
            hasExporter,
            hasSweeper,
            findings);
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
