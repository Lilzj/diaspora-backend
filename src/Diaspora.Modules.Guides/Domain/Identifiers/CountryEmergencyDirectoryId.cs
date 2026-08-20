using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one country's emergency information.
/// <para>
/// One aggregate per country rather than one per number, because the invariant that matters is
/// about the set: a country has one police number, and a directory showing two is worse than one
/// showing none. Editing the set as a unit is also how it is edited in practice — somebody checks
/// the whole page against an official source and saves it.
/// </para>
/// </summary>
public sealed record CountryEmergencyDirectoryId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryEmergencyDirectoryId New() => new(Guid.NewGuid());

    public static CountryEmergencyDirectoryId From(Guid value) => new(value);
}
