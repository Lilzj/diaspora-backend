namespace Diaspora.Modules.Community.Extensibility.Visibility;

/// <summary>
/// Runs every evaluator and composes the answers.
/// <para>
/// Deny wins, abstentions are ignored, and content with nothing but abstentions is
/// <b>denied</b>. That last rule is the one worth stating: if the only evaluator that
/// understood a custom audience is not registered, nobody sees the content. Defaulting to
/// visible would mean unregistering a module silently publishes everything that depended on it.
/// </para>
/// <para>
/// Unlike every other registry here, duplicates are fine. Several modules having an opinion
/// about who may read something is the expected case, and they compose safely because none of
/// them can widen.
/// </para>
/// </summary>
public sealed class VisibilityEvaluatorRegistry
{
    private readonly List<IVisibilityEvaluator> _evaluators;

    public VisibilityEvaluatorRegistry(IEnumerable<IVisibilityEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);

        _evaluators = evaluators
            .OrderBy(evaluator => evaluator.Order)
            .ThenBy(evaluator => evaluator.GetType().FullName, StringComparer.Ordinal)
            .ToList();
    }

    public int Count => _evaluators.Count;

    public VisibilityDecision Evaluate(VisibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var anyAllowed = false;

        foreach (var evaluator in _evaluators)
        {
            var decision = evaluator.Evaluate(request);

            if (decision.Denied)
            {
                return decision;
            }

            if (!decision.Abstained)
            {
                anyAllowed = true;
            }
        }

        return anyAllowed
            ? VisibilityDecision.Allow
            : VisibilityDecision.Deny("no-evaluator-allowed");
    }

    public bool IsVisible(VisibilityRequest request) => !Evaluate(request).Denied;
}
