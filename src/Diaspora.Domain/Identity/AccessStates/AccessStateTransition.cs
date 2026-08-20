namespace Diaspora.Domain.Identity.AccessStates;

/// <summary>
/// A single legal edge in the progressive access state machine.
/// </summary>
public sealed record AccessStateTransition(AccessState From, AccessState To)
{
    public override string ToString() => $"{From.Name} -> {To.Name}";
}
