namespace Diaspora.Modules.Media.Options;

/// <summary>
/// Which storage provider new objects go to, and where the built-in filesystem provider keeps
/// them.
/// <para>
/// Deliberately thin. A provider's own settings — an S3 region, an Azure connection string —
/// belong to that provider's options class in whichever assembly ships it, not here. Media
/// naming an S3 setting would be Media knowing about S3, which is the one thing the storage
/// abstraction exists to prevent.
/// </para>
/// </summary>
public sealed class MediaStorageOptions
{
    public const string SectionName = "Media:Storage";

    /// <summary>
    /// The provider new objects are written to. Empty selects the only registered provider, or
    /// the first by name when several are registered.
    /// <para>
    /// Changing this does not migrate anything: every existing object records the provider that
    /// wrote it and is still read through that provider. That is the property that makes a
    /// provider change a gradual operation with a way back.
    /// </para>
    /// </summary>
    public string DefaultProvider { get; set; } = FilesystemProviderName;

    /// <summary>The bucket, container or root the default provider writes to.</summary>
    public string Container { get; set; } = "diaspora-media";

    /// <summary>
    /// Where the built-in filesystem provider puts files. Ignored by every other provider.
    /// <para>
    /// The default is under the content root rather than a temp directory, because a temp
    /// directory is cleared on reboot and a member's profile photograph disappearing after a
    /// server restart is a bug that takes a week to diagnose.
    /// </para>
    /// </summary>
    public string FilesystemRoot { get; set; } = "media-storage";

    /// <summary>
    /// The base URL the API is reachable at, used to build links to content this deployment
    /// serves itself. Empty means links are relative, which is correct behind a single origin.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>The provider name the built-in filesystem implementation registers under.</summary>
    public const string FilesystemProviderName = "filesystem";
}
