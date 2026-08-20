namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// Pins an integration event's wire name and version independently of its CLR type.
/// <para>
/// <b>Optional, and deliberately so.</b> Every event shipped before this phase resolves by its
/// namespace-qualified type name at version 1, which is why B1.1 required no edit to any
/// module. The attribute exists for the two moments where the default is not enough:
/// </para>
/// <list type="number">
/// <item>
/// A type is moved or renamed. Without a pinned name, every row already in the outbox — and
/// every message a broker has not yet delivered — becomes unresolvable, and the refactor that
/// caused it will have passed code review as a rename.
/// </item>
/// <item>
/// A payload changes in a way a current consumer cannot read. The new shape ships as
/// version 2 beside a version 1 reader, both are resolvable, and consumers migrate on their
/// own schedule instead of during a deployment window.
/// </item>
/// </list>
/// <para>
/// Applying it to an existing type is itself a breaking change unless the name given matches
/// what the default already produced. That is the trade the attribute makes visible.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class IntegrationEventContractAttribute : Attribute
{
    public IntegrationEventContractAttribute(string name, int version = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        Name = name;
        Version = version;
    }

    public string Name { get; }

    public int Version { get; }
}
