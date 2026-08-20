using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Extensibility.Storage;

/// <summary>
/// Resolves a storage provider by name, and names the one new objects go to.
/// <para>
/// <b>Why resolution is by name rather than by "the configured one".</b> An object written in
/// 2026 to MinIO must still be readable in 2028 after the platform has moved to S3. The
/// <see cref="Domain.ValueObjects.StorageLocation"/> on every asset records which provider
/// wrote it; this registry hands back that provider, whatever the current default is. A read
/// path that used the configured provider instead would break every historical object the
/// moment the setting changed — which is the difference between a migration you can do
/// gradually and one that has to happen all at once with no way back.
/// </para>
/// <para>
/// Two providers claiming the same name is a configuration error, and it fails at construction
/// rather than resolving by assembly load order. A platform where which provider you got
/// depended on link order would be one where a deployment reads from one bucket and writes to
/// another without anyone noticing.
/// </para>
/// </summary>
public sealed class StorageProviderRegistry
{
    private readonly Dictionary<string, IMediaStorageProvider> _providers;
    private readonly string _defaultProviderName;

    public StorageProviderRegistry(
        IEnumerable<IMediaStorageProvider> providers, IOptions<MediaStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        _providers = new Dictionary<string, IMediaStorageProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            if (!_providers.TryAdd(provider.Name, provider))
            {
                throw new InvalidOperationException(
                    $"Two storage providers claim the name '{provider.Name}'. " +
                    "Provider names are written into every stored object's location and must " +
                    "be unique across the platform.");
            }
        }

        if (_providers.Count == 0)
        {
            throw new InvalidOperationException(
                "No media storage provider is registered. The module registers a filesystem " +
                "provider by default, so an empty registry means something removed it.");
        }

        var configured = options.Value.DefaultProvider;

        _defaultProviderName = !string.IsNullOrWhiteSpace(configured) &&
                               _providers.ContainsKey(configured)
            ? configured
            : _providers.Keys.Order(StringComparer.Ordinal).First();
    }

    /// <summary>Where new objects are written.</summary>
    public IMediaStorageProvider Default => _providers[_defaultProviderName];

    public IReadOnlyCollection<string> ProviderNames => _providers.Keys.ToList().AsReadOnly();

    /// <summary>
    /// The provider that wrote a given object.
    /// <para>
    /// A failure rather than an exception, and a distinct error code, because the operational
    /// meaning is specific: someone removed a provider a deployment still has objects in. That
    /// is a recoverable configuration mistake, and the caller should surface it as a service
    /// problem rather than a crash.
    /// </para>
    /// </summary>
    public Result<IMediaStorageProvider> Resolve(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return Result.Success(Default);
        }

        return _providers.TryGetValue(providerName, out var provider)
            ? Result.Success(provider)
            : Result.Failure<IMediaStorageProvider>(
                MediaErrors.StorageProviderUnknown(providerName));
    }

    /// <summary>Whether the default provider supports a capability.</summary>
    public bool DefaultSupports(StorageCapability capability) =>
        Default.Capabilities.HasFlag(capability);
}
