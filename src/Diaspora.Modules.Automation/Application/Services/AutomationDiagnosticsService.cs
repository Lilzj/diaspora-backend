using Diaspora.Modules.Automation.Domain.Errors;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Automation.Application.Services;

/// <summary>
/// Decides whose telemetry a caller is allowed to read.
/// <para>
/// <b>B24 · §24 · §25.</b> One place, because the answer is subtle and getting it wrong is a
/// cross-scope read. A run belongs to the *automation's* owner, which for an organization automation
/// is the organization rather than the moderator who wrote it — so "my telemetry" is not simply "runs
/// with my user id", and a diagnostics route that assumed it was would hide every scope-owned
/// automation's history from the people who administer it. Wave 12 already found and fixed that on
/// the history route; this exists so the fix is not re-derived, differently, on the second one.
/// </para>
/// <para>
/// <b>No route takes an owner parameter, and this is why it does not need to.</b> With no automation
/// named the subject is the caller's own identifier and nothing else. With one named, the scope
/// authority decides, and an automation the caller may not read is refused as
/// <see cref="AutomationErrors.NotFound"/> rather than as a denial — a distinguishable refusal on a
/// foreign identifier is an existence oracle, and the rest of this module already refuses that way.
/// </para>
/// </summary>
public sealed class AutomationDiagnosticsService
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;

    public AutomationDiagnosticsService(
        IAutomationRepository automations, AutomationAccessService access)
    {
        _automations = automations;
        _access = access;
    }

    /// <summary>
    /// Resolves the owner whose telemetry this caller may read, and the automation to narrow to.
    /// </summary>
    public async Task<Result<AutomationTelemetryScope>> ResolveAsync(
        UserId member, Guid? automationId, CancellationToken cancellationToken = default)
    {
        if (automationId is not { } requested)
        {
            return Result.Success(new AutomationTelemetryScope(member.Value, null));
        }

        var automation = await _automations.GetAsync(
            AutomationId.From(requested), cancellationToken);

        if (automation is null)
        {
            return Result.Failure<AutomationTelemetryScope>(AutomationErrors.NotFound());
        }

        var scope = await _access.AuthorizeScopeAsync(
            member, automation.OwnerKind, automation.OwnerId, cancellationToken);

        return scope.IsFailure
            ? Result.Failure<AutomationTelemetryScope>(AutomationErrors.NotFound())
            : Result.Success(new AutomationTelemetryScope(automation.OwnerId, automation.Id));
    }
}

/// <summary>Whose runs, and which automation's.</summary>
/// <param name="OwnerId">The owner every query below filters on, unconditionally.</param>
/// <param name="AutomationId">One automation, when the caller named one they may read.</param>
public sealed record AutomationTelemetryScope(Guid OwnerId, AutomationId? AutomationId);
