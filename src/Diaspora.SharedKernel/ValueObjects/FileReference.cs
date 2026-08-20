using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class FileReference : ValueObject
{
    public string StorageKey { get; }
    public string FileName { get; }
    public string MimeType { get; }
    public long SizeBytes { get; }

    private FileReference(string storageKey, string fileName, string mimeType, long sizeBytes)
    {
        StorageKey = storageKey;
        FileName = fileName;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
    }

    public static FileReference Create(string storageKey, string fileName, string mimeType, long sizeBytes)
    {
        Guard.Against.NullOrWhiteSpace(storageKey, nameof(storageKey));
        Guard.Against.NullOrWhiteSpace(fileName, nameof(fileName));
        Guard.Against.NullOrWhiteSpace(mimeType, nameof(mimeType));
        Guard.Against.OutOfRange(sizeBytes, nameof(sizeBytes), 0, long.MaxValue);
        return new FileReference(storageKey, fileName, mimeType, sizeBytes);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StorageKey;
        yield return FileName;
        yield return MimeType;
        yield return SizeBytes;
    }
}
