using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>A posting's hiring pipeline identity.</summary>
public sealed record HiringPipelineId(Guid Value) : StronglyTypedId(Value)
{
    public static HiringPipelineId New() => new(Guid.NewGuid());

    public static HiringPipelineId From(Guid value) => new(value);
}
