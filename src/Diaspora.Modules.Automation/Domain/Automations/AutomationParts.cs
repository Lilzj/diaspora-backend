using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Automation.Domain.Automations;

/// <summary>
/// One IF on one automation.
/// <para>
/// <b>A key, an operator and up to two scalars. That is the entire expressive power.</b> There is no
/// nesting, no boolean algebra beyond the implicit AND across the list, no reference to another
/// condition and no way to write a comparison the catalogue did not define. An automation cannot
/// express arbitrary logic because there is nowhere in this shape to put any.
/// </para>
/// <para>
/// A child entity with its own key rather than an owned collection: EF invents a store-generated
/// shadow integer inside a composite primary key for an unkeyed owned collection, and the insert
/// fails on the second write rather than the first — which is why that defect survives every test
/// that starts from an empty table.
/// </para>
/// </summary>
public sealed class AutomationConditionRule : Entity<AutomationConditionId>
{
    private AutomationConditionRule() { }

    private AutomationConditionRule(AutomationConditionId id) : base(id) { }

    public AutomationId AutomationId { get; private set; } = null!;

    /// <summary>A key from the catalogue. Re-checked at execution, because a stored rule is untrusted.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>One of the operators that key declares. Anything else is refused on write.</summary>
    public string Operator { get; private set; } = string.Empty;

    /// <summary>
    /// The comparison value, as text.
    /// <para>
    /// One column rather than one per kind, because the catalogue's <c>valueKind</c> already says how
    /// to read it and a column per kind would be twelve mostly-null columns and a rule about which one
    /// is authoritative.
    /// </para>
    /// </summary>
    public string? Value { get; private set; }

    /// <summary><c>between</c> and <c>within</c> use both; every other operator leaves this null.</summary>
    public string? SecondaryValue { get; private set; }

    /// <summary>Preserves the order the member wrote them in. Conditions are ANDed, so it is display order.</summary>
    public int Position { get; private set; }

    /// <summary>
    /// Builds one condition.
    /// <para>
    /// <paramref name="id"/> is supplied rather than generated when the caller is editing: the
    /// builder holds stable ids so it can reorder and remove, and an edit that renumbered them would
    /// orphan every run outcome that referenced one.
    /// </para>
    /// </summary>
    internal static AutomationConditionRule Create(
        AutomationId automationId,
        AutomationConditionId? id,
        string key,
        string @operator,
        string? value,
        string? secondaryValue,
        int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(@operator);

        return new AutomationConditionRule(id ?? AutomationConditionId.New())
        {
            AutomationId = automationId,
            Key = key,
            Operator = @operator,
            Value = Cap(value),
            SecondaryValue = Cap(secondaryValue),
            Position = position
        };
    }

    private static string? Cap(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= 512 ? value : value[..512];
}

/// <summary>
/// One THEN on one automation.
/// <para>
/// <b>A key from the allow-list and a bag of scalars.</b> The parameters carry which reminder offset,
/// which deep link, which template id — never an amount, never an account, never a recipient list.
/// The recipient set is derived from the scope by the engine at execution, which is what §49 requires
/// and what stops the client deciding who an automation reaches.
/// </para>
/// </summary>
public sealed class AutomationActionStep : Entity<AutomationActionId>
{
    private AutomationActionStep() { }

    private AutomationActionStep(AutomationActionId id) : base(id) { }

    public AutomationId AutomationId { get; private set; } = null!;

    public string Key { get; private set; } = string.Empty;

    /// <summary>Serialized scalars. Capped, and rejected on write when it exceeds the cap.</summary>
    public string Parameters { get; private set; } = "{}";

    /// <summary>
    /// The order actions are attempted in.
    /// <para>
    /// Attempted rather than executed: one failing action does not stop the next. Retry is per action
    /// rather than per run, because retrying a whole run re-sends the actions that already succeeded.
    /// </para>
    /// </summary>
    public int Position { get; private set; }

    internal static AutomationActionStep Create(
        AutomationId automationId,
        AutomationActionId? id,
        string key,
        string parameters,
        int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new AutomationActionStep(id ?? AutomationActionId.New())
        {
            AutomationId = automationId,
            Key = key,
            Parameters = string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters,
            Position = position
        };
    }
}

/// <summary>
/// One entry in an automation's audit trail. Append-only.
/// <para>
/// <b>The actor is stored as an identifier and a label, and the label is the one that is read.</b>
/// §51 requires "changed by a former moderator" to still be answerable, which a role lookup at read
/// time cannot do — the role is gone. The display name is captured at the moment of the change and
/// never refreshed, which is also why it is never an email and never an internal identifier: an
/// audit trail is a member-facing surface with a long retention.
/// </para>
/// </summary>
public sealed class AutomationAuditRecord : Entity<AutomationAuditId>
{
    private AutomationAuditRecord() { }

    private AutomationAuditRecord(AutomationAuditId id) : base(id) { }

    public AutomationId AutomationId { get; private set; } = null!;

    public AutomationAuditAction Action { get; private set; }

    /// <summary>Empty when the platform itself acted — an expiry, a revoked capability.</summary>
    public UserId ActorUserId { get; private set; } = UserId.Empty;

    /// <summary>A display name, captured at the time. Never an email, never an internal id.</summary>
    public string ActorLabel { get; private set; } = string.Empty;

    public DateTimeOffset At { get; private set; }

    /// <summary>What changed, in the member's words. Never a rule's parameter values.</summary>
    public string? Detail { get; private set; }

    internal static AutomationAuditRecord Create(
        AutomationId automationId,
        AutomationAuditAction action,
        UserId actor,
        string actorLabel,
        DateTimeOffset at,
        string? detail) =>
        new(AutomationAuditId.New())
        {
            AutomationId = automationId,
            Action = action,
            ActorUserId = actor,
            ActorLabel = string.IsNullOrWhiteSpace(actorLabel) ? "Someone" : actorLabel.Trim(),
            At = at,
            Detail = string.IsNullOrWhiteSpace(detail) ? null
                : detail.Length <= 300 ? detail : detail[..300]
        };
}
