using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>Identifies one explicit access grant over an asset.</summary>
public sealed record MediaGrantId(Guid Value) : StronglyTypedId(Value)
{
    public static MediaGrantId New() => new(Guid.NewGuid());

    public static MediaGrantId From(Guid value) => new(value);
}
