using System.Text.Json;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Errors;
using Diaspora.Modules.Automation.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.Services;

/// <summary>One condition as a caller supplied it, before it is anything.</summary>
public readonly record struct ConditionInput(
    Guid? Id, string Key, string Operator, string? Value, string? SecondaryValue);

/// <summary>One action as a caller supplied it.</summary>
public readonly record struct ActionInput(Guid? Id, string Key, string? Parameters);

/// <summary>
/// Checks a rule against the catalogue, before anything stores it or runs it.
/// <para>
/// <b>This is where "the server re-validates the rule against the catalogue on every write" lives, and
/// it is the reason a permissive registry cannot turn an unauthorized action key into an authorized
/// one later.</b> A client that invents a trigger key, an action key, an operator or a capability
/// receives 400. The rule is untrusted input every time it is read, not only when it is written.
/// </para>
/// <para>
/// <b>Nothing here parses an expression, and there is nothing to parse.</b> A parameter bag is JSON
/// whose values must be scalars; a nested object or an array of objects is refused. That refusal is
/// not tidiness — a nested structure is where a rules language starts, and §7 forbids exposing one to
/// members.
/// </para>
/// <para>
/// <b>A trigger the platform cannot observe may be saved as a draft and may not be activated.</b> The
/// fail-closed reading: a member may keep a rule for a capability that has not shipped, and switching
/// it on is refused with a 400 naming what is missing — because a rule that is on and will never fire
/// is how a member concludes the platform is broken.
/// </para>
/// </summary>
public sealed class AutomationRuleValidator
{
    private readonly IOptions<AutomationOptions> _options;

    public AutomationRuleValidator(IOptions<AutomationOptions> options) => _options = options;

    /// <summary>
    /// Validates a whole rule for an owner kind, and for whether it is being switched on.
    /// </summary>
    public Result Validate(
        string triggerKey,
        string? triggerParameters,
        IReadOnlyList<ConditionInput> conditions,
        IReadOnlyList<ActionInput> actions,
        AutomationOwnerKind ownerKind,
        IReadOnlyCollection<string> grantedCapabilities,
        bool activating)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(grantedCapabilities);

        var trigger = AutomationCatalog.FindTrigger(triggerKey);

        if (trigger is null)
        {
            return Result.Failure(AutomationErrors.UnknownTrigger(triggerKey));
        }

        if (!trigger.AllowedOwners.Contains(ownerKind))
        {
            return Result.Failure(
                AutomationErrors.OwnerNotAllowed(trigger.Key, ownerKind.ToString()));
        }

        if (activating && !trigger.IsAvailable)
        {
            return Result.Failure(
                AutomationErrors.TriggerUnavailable(trigger.Key, trigger.UnavailableReason));
        }

        if (trigger.RequiredCapability is { } triggerCapability &&
            !grantedCapabilities.Contains(triggerCapability, StringComparer.Ordinal))
        {
            return Result.Failure(AutomationErrors.CapabilityRequired(triggerCapability));
        }

        if (ParameterBagIsInvalid(triggerParameters, out var triggerFailure))
        {
            return Result.Failure(AutomationErrors.InvalidSchedule(triggerFailure!));
        }

        if (conditions.Count > _options.Value.MaximumConditionsPerAutomation)
        {
            return Result.Failure(
                AutomationErrors.InvalidSchedule(
                    $"An automation may have at most {_options.Value.MaximumConditionsPerAutomation} "
                    + "checks."));
        }

        foreach (var condition in conditions)
        {
            var definition = AutomationCatalog.FindCondition(condition.Key);

            if (definition is null)
            {
                return Result.Failure(AutomationErrors.UnknownCondition(condition.Key));
            }

            if (!definition.Operators.Contains(condition.Operator, StringComparer.Ordinal))
            {
                return Result.Failure(
                    AutomationErrors.UnknownOperator(condition.Key, condition.Operator));
            }

            if ((condition.Value?.Length ?? 0) > 512 || (condition.SecondaryValue?.Length ?? 0) > 512)
            {
                return Result.Failure(AutomationErrors.PayloadTooLarge("comparison value"));
            }
        }

        if (actions.Count == 0)
        {
            return Result.Failure(AutomationErrors.NoActions());
        }

        if (actions.Count > _options.Value.MaximumActionsPerAutomation)
        {
            return Result.Failure(
                AutomationErrors.InvalidSchedule(
                    $"An automation may do at most {_options.Value.MaximumActionsPerAutomation} "
                    + "things."));
        }

        foreach (var action in actions)
        {
            var definition = AutomationCatalog.FindAction(action.Key);

            if (definition is null)
            {
                return Result.Failure(AutomationErrors.UnknownAction(action.Key));
            }

            if (!definition.AllowedOwners.Contains(ownerKind))
            {
                return Result.Failure(
                    AutomationErrors.OwnerNotAllowed(definition.Key, ownerKind.ToString()));
            }

            if (activating && !definition.IsAvailable)
            {
                return Result.Failure(
                    AutomationErrors.ActionUnavailable(definition.Key, definition.UnavailableReason));
            }

            // The capability check on the write path. It is checked again at dispatch, and both are
            // needed: this one stops a rule being written that could never run, and the dispatch one
            // stops a rule that could run yesterday running today.
            if (definition.RequiredCapability is { } capability &&
                !grantedCapabilities.Contains(capability, StringComparer.Ordinal))
            {
                return Result.Failure(AutomationErrors.CapabilityRequired(capability));
            }

            if (ParameterBagIsInvalid(action.Parameters, out var actionFailure))
            {
                return Result.Failure(AutomationErrors.InvalidSchedule(actionFailure!));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Whether a parameter bag is something this platform will store.
    /// <para>
    /// A flat JSON object whose values are scalars or arrays of scalars, under the length ceiling.
    /// Everything else is refused: an object inside an object is the beginning of a language, and the
    /// only reliable time to refuse it is before it is stored.
    /// </para>
    /// </summary>
    private bool ParameterBagIsInvalid(string? json, out string? failure)
    {
        failure = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        if (json.Length > _options.Value.MaximumParameterLength)
        {
            failure = "That automation carries more settings than the platform accepts.";
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                failure = "An automation's settings must be a simple set of named values.";
                return true;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Object)
                {
                    failure = "An automation's settings cannot contain nested structures.";
                    return true;
                }

                if (property.Value.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var element in property.Value.EnumerateArray())
                {
                    if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        failure = "An automation's settings cannot contain nested structures.";
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            failure = "Those automation settings are not something the platform can read.";
            return true;
        }

        return false;
    }
}
