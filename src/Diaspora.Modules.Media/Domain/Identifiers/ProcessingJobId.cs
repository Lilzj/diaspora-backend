using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>Identifies one unit of post-upload work over an asset.</summary>
public sealed record ProcessingJobId(Guid Value) : StronglyTypedId(Value)
{
    public static ProcessingJobId New() => new(Guid.NewGuid());

    public static ProcessingJobId From(Guid value) => new(value);
}
