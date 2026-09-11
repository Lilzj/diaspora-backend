using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Automation.Application.Engine;

/// <summary>
/// Asks the owning modules the questions a payload cannot answer — Wave 12.
/// <para>
/// <b>It runs before evaluation, never during it.</b> The evaluator stays what it was: pure, static,
/// synchronous, reaching nothing. Everything it compares is in a dictionary by the time it is called,
/// and this is what fills the half of that dictionary the trigger did not carry. Making the evaluator
/// async instead would have put an I/O boundary inside the one class whose safety argument is that it
/// cannot reach anything.
/// </para>
/// <para>
/// <b>Nothing is asked unless a rule asks it.</b> The keys come from the automation's own conditions,
/// filtered to those the catalogue marks as externally sourced. A rule with no such condition makes no
/// call at all, which is the overwhelming majority of runs.
/// </para>
/// <para>
/// <b>A source that throws is a condition that fails closed, not a run that crashes.</b> An owning
/// module having a bad minute must produce a skipped run saying the check could not be made — the
/// answer the member can act on — rather than an exception inside a background consumer that a
/// redelivery would only reproduce.
/// </para>
/// </summary>
public sealed class AutomationConditionResolver
{
    private readonly IReadOnlyList<IAutomationConditionSource> _sources;
    private readonly ILogger<AutomationConditionResolver> _logger;

    public AutomationConditionResolver(
        IEnumerable<IAutomationConditionSource> sources,
        ILogger<AutomationConditionResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = [.. sources];
        _logger = logger;
    }

    /// <summary>
    /// The externally-sourced values this rule needs, or an empty dictionary.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string?>> ResolveAsync(
        IEnumerable<AutomationConditionRule> conditions,
        UserId member,
        Guid? subjectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(member);

        var resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var wanted = conditions
            .Select(condition => AutomationCatalog.FindCondition(condition.Key))
            .Where(definition => definition is { ExternalSource: not null })
            .Select(definition => definition!.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (wanted.Length == 0 || _sources.Count == 0 || member == UserId.Empty)
        {
            return resolved;
        }

        foreach (var source in _sources)
        {
            var keys = wanted
                .Where(key => source.Answers.Contains(key, StringComparer.Ordinal))
                .Where(key => !resolved.ContainsKey(key))
                .ToArray();

            // ── Asked, and the asking is recorded ───────────────────────────────────────────────
            //
            // A null entry means "a source for this key exists in this deployment and had no answer
            // for this member"; an absent entry means "no source for it exists at all". The
            // evaluator tells a member two different things for those, and before Phase 5 it could
            // not tell them apart: the resolver dropped nulls, so a creator whose analytics rollup
            // had not been computed was told the platform has no analytics at all.
            //
            // Written before the call rather than after, so a source that throws leaves the same
            // mark as one that answers nothing — both are "we asked and got nothing", which is what
            // the member needs to know.
            foreach (var key in keys)
            {
                resolved[key] = null;
            }

            if (keys.Length == 0)
            {
                continue;
            }

            try
            {
                var answers = await source.ReadAsync(
                    new AutomationConditionQuery(member, keys, subjectId), cancellationToken);

                foreach (var answer in answers)
                {
                    // A source answering a key it did not declare is dropped rather than trusted. The
                    // declaration is what the resolver routed on, and honouring an undeclared answer
                    // would let one module quietly take over another's condition.
                    if (answer.Value is null ||
                        !source.Answers.Contains(answer.Key, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    resolved[answer.Key] = answer.Value;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // A source that failed must skip the run, never crash the consumer.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "An automation condition source could not answer {Keys}. The conditions it owns "
                    + "will fail closed.",
                    string.Join(", ", keys));
            }
#pragma warning restore CA1031
        }

        return resolved;
    }
}
