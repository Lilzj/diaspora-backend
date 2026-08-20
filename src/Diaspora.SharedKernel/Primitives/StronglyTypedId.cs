namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// Base class for strongly typed IDs. Eliminates primitive obsession
/// by wrapping Guid identifiers in domain-specific types.
/// </summary>
public abstract record StronglyTypedId(Guid Value)
{
    public override string ToString() => Value.ToString();

    public static implicit operator Guid(StronglyTypedId id) => id.Value;
}
