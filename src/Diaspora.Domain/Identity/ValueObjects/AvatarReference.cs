using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.ValueObjects;

/// <summary>
/// A pointer to the member's avatar in blob storage. Identity stores the reference
/// only — upload, resizing and CDN URL construction belong to the media subsystem.
/// </summary>
public sealed class AvatarReference : ValueObject
{
    public const int MaxLength = 512;

    public string StorageKey { get; }

    private AvatarReference(string storageKey)
    {
        StorageKey = storageKey;
    }

    public static AvatarReference Create(string storageKey)
    {
        Guard.Against.NullOrWhiteSpace(storageKey, nameof(storageKey));
        var normalized = storageKey.Trim();
        Guard.Against.MaxLength(normalized, MaxLength, nameof(storageKey));
        return new AvatarReference(normalized);
    }

    public static bool TryCreate(string? storageKey, out AvatarReference? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(storageKey)) return false;

        var normalized = storageKey.Trim();
        if (normalized.Length > MaxLength) return false;

        result = new AvatarReference(normalized);
        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StorageKey;
    }

    public override string ToString() => StorageKey;
}
