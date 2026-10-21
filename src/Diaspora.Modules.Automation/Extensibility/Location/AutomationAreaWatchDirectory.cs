using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Automation.Extensibility.Location;

/// <summary>
/// Answers Location's "does anybody's rule care that this member entered these areas" — 21 Oct 2026.
/// <para>
/// Reads the member's own active <c>location.enteredArea</c> automations and the areas their
/// <c>neighbourhood</c> conditions name. A rule with no area to include — no neighbourhood condition,
/// or only an exclusion — fires on entering any area, so it watches every area.
/// </para>
/// <para>
/// <b>Only the member's own rules.</b> The trigger is <c>UserOnly</c>, so the owner is the member;
/// nobody else's automation may cause a fact about this member's movements to be published.
/// </para>
/// </summary>
public sealed class AutomationAreaWatchDirectory : IAutomationAreaWatchDirectory
{
    private const string TriggerKey = "location.enteredArea";

    private readonly DiasporaDbContext _dbContext;

    public AutomationAreaWatchDirectory(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public async Task<AutomationAreaWatch> GetAsync(
        Guid memberId, CancellationToken cancellationToken = default)
    {
        if (memberId == Guid.Empty)
        {
            return AutomationAreaWatch.Nothing;
        }

        var automations = await _dbContext.Set<AutomationDefinition>()
            .AsNoTracking()
            .Include(automation => automation.Conditions)
            .Where(automation =>
                automation.TriggerKey == TriggerKey
                && automation.Status == AutomationStatus.Active
                && automation.OwnerKind == AutomationOwnerKind.User
                && automation.OwnerId == memberId)
            .Take(100)
            .ToListAsync(cancellationToken);

        var watchesAny = false;
        var areas = new HashSet<Guid>();

        foreach (var automation in automations)
        {
            var named = automation.Conditions
                .Where(condition =>
                    condition.Key == "neighbourhood"
                    && condition.Operator is "equals" or "includes")
                .SelectMany(condition => (condition.Value ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            if (named.Count == 0)
            {
                watchesAny = true;
            }

            areas.UnionWith(named);
        }

        return new AutomationAreaWatch(watchesAny, areas);
    }
}
