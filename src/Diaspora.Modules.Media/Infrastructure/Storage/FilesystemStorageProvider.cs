using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Infrastructure.Storage;

/// <summary>
/// The storage provider Media ships with: the local filesystem.
/// <para>
/// <b>Why a filesystem provider at all, rather than an S3 one.</b> Media must not reference a
/// cloud SDK — that would put a provider inside the abstraction that exists to keep providers
/// out. It must also not ship with <em>nothing</em>, because then the module does not work
/// until somebody writes a provider, and every test, every development machine and every
/// evaluation deployment is blocked on that. A filesystem provider needs no package, works
/// everywhere, and is a genuinely correct choice for a single-instance deployment.
/// </para>
/// <para>
/// <b>What it honestly cannot do.</b> It declares only <c>RangeReads</c>. It cannot mint a
/// presigned URL, because there is nothing to presign — content is served through the API. It
/// cannot compose server-side, so assembly streams through the application. Declaring
/// capabilities it does not have would be worse than lacking them: the pipeline would plan a
/// presigned upload and the client would PUT to a URL that does not exist.
/// </para>
/// <para>
/// <b>Path safety.</b> Every key is validated by <see cref="StorageKey"/> — no traversal
/// segments, no absolute paths, no backslashes — and every resolved path is then checked to be
/// under the root before it is opened. Both, because the first is the rule and the second is
/// what catches the day the rule is bypassed by a migration that wrote a key directly.
/// </para>
/// </summary>
public sealed class FilesystemStorageProvider : IMediaStorageProvider
{
    private readonly string _root;
    private readonly MediaStorageOptions _options;
    private readonly ILogger<FilesystemStorageProvider> _logger;

    public FilesystemStorageProvider(
        IOptions<MediaStorageOptions> options, ILogger<FilesystemStorageProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;

        _root = Path.GetFullPath(
            Path.IsPathRooted(_options.FilesystemRoot)
                ? _options.FilesystemRoot
                : Path.Combine(AppContext.BaseDirectory, _options.FilesystemRoot));
    }

    public string Name => MediaStorageOptions.FilesystemProviderName;

    public string DefaultContainer => _options.Container;

    /// <summary>
    /// Range reads only. Everything else this provider genuinely cannot do, and saying so is
    /// what lets the pipeline plan around it.
    /// </summary>
    public StorageCapability Capabilities => StorageCapability.RangeReads;

    public async Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = ResolvePath(request.Container, request.Key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written to a temporary name and moved into place, so a crash mid-write leaves no
        // half-file that a reader would happily serve. The move is atomic within a volume,
        // which is the guarantee the whole write path depends on.
        var staging = $"{path}.{Guid.NewGuid():N}.partial";

        long written;

        try
        {
            await using (var destination = new FileStream(
                staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true))
            {
                await request.Content.CopyToAsync(destination, cancellationToken);
                written = destination.Length;
            }

            File.Move(staging, path, overwrite: true);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }

        return new StorageWriteResult
        {
            Key = request.Key,
            SizeInBytes = written
        };
    }

    public Task<Stream?> OpenReadAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(container, key);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public async Task<Stream?> OpenRangeAsync(
        string container,
        StorageKey key,
        long offset,
        long? length,
        CancellationToken cancellationToken = default)
    {
        var stream = await OpenReadAsync(container, key, cancellationToken);

        if (stream is null)
        {
            return null;
        }

        if (offset <= 0 && length is null)
        {
            return stream;
        }

        if (offset >= stream.Length)
        {
            await stream.DisposeAsync();
            return null;
        }

        stream.Seek(offset, SeekOrigin.Begin);

        return length is null
            ? stream
            : new BoundedReadStream(stream, Math.Min(length.Value, stream.Length - offset));
    }

    public Task<bool> ExistsAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePath(container, key)));

    public Task<StorageObjectInfo?> DescribeAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(container, key);

        if (!File.Exists(path))
        {
            return Task.FromResult<StorageObjectInfo?>(null);
        }

        var info = new FileInfo(path);

        return Task.FromResult<StorageObjectInfo?>(new StorageObjectInfo
        {
            SizeInBytes = info.Length,
            LastModified = info.LastWriteTimeUtc
        });
    }

    public Task DeleteAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default)
    {
        TryDelete(ResolvePath(container, key));
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(
        string container, string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Task.CompletedTask;
        }

        // The prefix goes through the same validation a key does, so a caller cannot ask this
        // to recursively delete a directory outside the root.
        var validated = StorageKey.TryCreate($"{prefix.TrimEnd('/')}/placeholder");

        if (validated.IsFailure)
        {
            return Task.CompletedTask;
        }

        var directory = Path.GetDirectoryName(ResolvePath(container, validated.Value));

        if (directory is not null && Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException exception)
            {
                _logger.LogWarning(
                    exception, "Could not remove media prefix {Prefix}.", prefix);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Not supported: there is no signable endpoint in front of a directory. Content is served
    /// through the API instead, which every deployment supports.
    /// </summary>
    public Task<Uri?> CreatePresignedReadUrlAsync(
        string container, StorageKey key, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        Task.FromResult<Uri?>(null);

    /// <summary>Not supported. The client uploads through the API.</summary>
    public Task<Uri?> CreatePresignedWriteUrlAsync(
        string container,
        StorageKey key,
        MediaContentType contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Uri?>(null);

    /// <summary>
    /// Concatenates the staged parts. Streamed rather than buffered: assembling a 512 MB video
    /// by reading it into memory would be 512 MB of managed heap per concurrent completion.
    /// </summary>
    public async Task<StorageWriteResult> ComposeAsync(
        string container,
        IReadOnlyList<StorageKey> parts,
        StorageKey destination,
        MediaContentType contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(destination);

        var path = ResolvePath(container, destination);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var staging = $"{path}.{Guid.NewGuid():N}.partial";
        long written;

        try
        {
            await using (var target = new FileStream(
                staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true))
            {
                foreach (var part in parts)
                {
                    var partPath = ResolvePath(container, part);

                    if (!File.Exists(partPath))
                    {
                        throw new FileNotFoundException(
                            "An upload part is missing from storage.", part.Value);
                    }

                    await using var source = new FileStream(
                        partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);

                    await source.CopyToAsync(target, cancellationToken);
                }

                written = target.Length;
            }

            File.Move(staging, path, overwrite: true);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }

        return new StorageWriteResult
        {
            Key = destination,
            SizeInBytes = written
        };
    }

    /// <summary>
    /// Healthy when the root exists and is writable. Checked by writing, because a directory
    /// that exists and is read-only reports healthy on an existence check and fails on the
    /// first upload.
    /// </summary>
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_root);

            var probe = Path.Combine(_root, $".health-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);

            return Task.FromResult(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Media filesystem storage at {Root} is not writable.", _root);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Maps a container and key to a path, and refuses anything that escapes the root.
    /// <para>
    /// Belt and braces. <see cref="StorageKey"/> already rejects traversal segments, so this
    /// check should never fire — which is exactly why it is here. The day it does fire is the
    /// day something wrote a key past the value object, and the failure needs to be a thrown
    /// exception rather than a file read from outside the store.
    /// </para>
    /// </summary>
    private string ResolvePath(string container, StorageKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var safeContainer = string.IsNullOrWhiteSpace(container)
            ? DefaultContainer
            : container.Replace('\\', '-').Replace('/', '-').Trim();

        var combined = Path.GetFullPath(
            Path.Combine(_root, safeContainer, key.Value.Replace('/', Path.DirectorySeparatorChar)));

        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "A media storage key resolved outside the configured root.");
        }

        return combined;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A file we could not remove is a sweeper's problem, not a caller's.
        }
    }
}
