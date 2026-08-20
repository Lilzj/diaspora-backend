using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>Identifies one step inside a processing job.</summary>
public sealed record ProcessingTaskId(Guid Value) : StronglyTypedId(Value)
{
    public static ProcessingTaskId New() => new(Guid.NewGuid());

    public static ProcessingTaskId From(Guid value) => new(value);
}
