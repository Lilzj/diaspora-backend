using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>Identifies one received part of a chunked upload.</summary>
public sealed record UploadChunkId(Guid Value) : StronglyTypedId(Value)
{
    public static UploadChunkId New() => new(Guid.NewGuid());

    public static UploadChunkId From(Guid value) => new(value);
}
