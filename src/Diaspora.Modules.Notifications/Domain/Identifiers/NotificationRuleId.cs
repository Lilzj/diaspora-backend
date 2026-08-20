using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>
/// One routing rule: which published fact becomes which message, on which channels, how urgently.
/// <para>
/// A rule has an identity of its own because it is reference data an operator edits, not a constant
/// in a switch statement. That is the difference between "routing is policy-driven" and "routing is
/// hard-coded and we wrote a comment saying it is not".
/// </para>
/// </summary>
public sealed record NotificationRuleId(Guid Value) : StronglyTypedId(Value)
{
    public static NotificationRuleId New() => new(Guid.NewGuid());

    public static NotificationRuleId From(Guid value) => new(value);
}
