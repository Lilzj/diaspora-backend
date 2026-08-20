using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Extensibility.Storage;

/// <summary>What a provider knows about an object without reading it.</summary>
public sealed record StorageObjectInfo
{
    public required long SizeInBytes { get; init; }

    public required DateTimeOffset LastModified { get; init; }

    public string? ContentType { get; init; }

    public string? ETag { get; init; }

    public StorageTier Tier { get; init; } = StorageTier.Hot;
}
