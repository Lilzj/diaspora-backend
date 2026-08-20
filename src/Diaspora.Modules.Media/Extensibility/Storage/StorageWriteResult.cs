using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Storage;

/// <summary>
/// What a provider reports after writing an object.
/// <para>
/// The size is the provider's count, not the caller's expectation, and the difference matters:
/// a truncated write that the provider accepted reports a smaller number, and comparing the
/// two is how the caller notices. A write path that trusts its own expectation cannot detect
/// a partial write at all.
/// </para>
/// </summary>
public sealed record StorageWriteResult
{
    public required StorageKey Key { get; init; }

    /// <summary>What the provider actually stored.</summary>
    public required long SizeInBytes { get; init; }

    /// <summary>
    /// The provider's own entity tag, when it returns one. Recorded for provider-side
    /// integrity checks; the platform's own integrity story is <see cref="ContentHash"/>,
    /// because an ETag means different things on different providers and nothing at all on
    /// some.
    /// </summary>
    public string? ETag { get; init; }
}
