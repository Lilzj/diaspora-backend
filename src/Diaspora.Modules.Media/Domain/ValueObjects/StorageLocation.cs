using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// Everything needed to find one stored object again: which provider, which container, which
/// key, which tier.
/// <para>
/// The provider is recorded per object rather than read from configuration, and that is the
/// single decision that makes migrating between providers survivable. A deployment that moves
/// from MinIO to S3 flips the configured default; every object written before the flip still
/// says <c>minio</c> and is still readable, because the read path resolves the provider from
/// the object rather than from the current setting. Configuration-derived provenance would
/// mean the migration is a big-bang copy with no rollback.
/// </para>
/// <para>
/// No URL appears here. A URL is minted at the read boundary from the key and expires; storing
/// one would put a permanently-live link to an immigration document in a database column.
/// </para>
/// </summary>
public sealed class StorageLocation : ValueObject
{
    public const int MaxProviderLength = 64;

    public const int MaxContainerLength = 128;

    private StorageLocation(string provider, string container, StorageKey key, StorageTier tier)
    {
        Provider = provider;
        Container = container;
        Key = key;
        Tier = tier;
    }

    /// <summary>The provider's registered name: <c>filesystem</c>, <c>s3</c>, <c>azure-blob</c>.</summary>
    public string Provider { get; }

    /// <summary>Bucket, container or root directory.</summary>
    public string Container { get; }

    public StorageKey Key { get; }

    public StorageTier Tier { get; }

    public static Result<StorageLocation> TryCreate(
        string? provider, string? container, StorageKey key, StorageTier tier = StorageTier.Hot)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (string.IsNullOrWhiteSpace(provider) || provider.Length > MaxProviderLength)
        {
            return Result.Failure<StorageLocation>(Error.Validation(
                MediaErrorCodes.StorageProviderUnknown, "A storage provider name is required."));
        }

        if (string.IsNullOrWhiteSpace(container) || container.Length > MaxContainerLength)
        {
            return Result.Failure<StorageLocation>(Error.Validation(
                MediaErrorCodes.StorageProviderUnknown, "A storage container is required."));
        }

        return Result.Success(new StorageLocation(
            provider.Trim().ToLowerInvariant(), container.Trim(), key, tier));
    }

    public static StorageLocation Create(
        string provider, string container, StorageKey key, StorageTier tier = StorageTier.Hot) =>
        TryCreate(provider, container, key, tier) is { IsSuccess: true } result
            ? result.Value
            : throw new ArgumentException("Not a usable storage location.", nameof(provider));

    /// <summary>The same object, recorded as having moved to another tier.</summary>
    public StorageLocation InTier(StorageTier tier) =>
        tier == Tier ? this : new StorageLocation(Provider, Container, Key, tier);

    /// <summary>A sibling object in the same provider and container.</summary>
    public StorageLocation Sibling(StorageKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new StorageLocation(Provider, Container, key, Tier);
    }

    public override string ToString() => $"{Provider}://{Container}/{Key.Value}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Provider;
        yield return Container;
        yield return Key;
        yield return Tier;
    }
}
