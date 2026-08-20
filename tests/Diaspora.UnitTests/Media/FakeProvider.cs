using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Storage;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// A storage provider that does nothing, for testing the registry's resolution rules.
/// <para>
/// The registry's job is name resolution and collision detection, and neither needs a provider
/// that stores anything. A test double that actually wrote files would make these tests slower
/// and would test the wrong thing.
/// </para>
/// </summary>
internal sealed class FakeProvider : IMediaStorageProvider
{
    public FakeProvider(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string DefaultContainer => "test";

    public StorageCapability Capabilities => StorageCapability.None;

    public Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new StorageWriteResult { Key = request.Key, SizeInBytes = 0 });

    public Task<Stream?> OpenReadAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(null);

    public Task<Stream?> OpenRangeAsync(
        string container,
        StorageKey key,
        long offset,
        long? length,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(null);

    public Task<bool> ExistsAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<StorageObjectInfo?> DescribeAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default) =>
        Task.FromResult<StorageObjectInfo?>(null);

    public Task DeleteAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeletePrefixAsync(
        string container, string prefix, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Uri?> CreatePresignedReadUrlAsync(
        string container,
        StorageKey key,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Uri?>(null);

    public Task<Uri?> CreatePresignedWriteUrlAsync(
        string container,
        StorageKey key,
        MediaContentType contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Uri?>(null);

    public Task<StorageWriteResult> ComposeAsync(
        string container,
        IReadOnlyList<StorageKey> parts,
        StorageKey destination,
        MediaContentType contentType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new StorageWriteResult { Key = destination, SizeInBytes = 0 });

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
