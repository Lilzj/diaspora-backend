using System.Text.Json;
using System.Text.Json.Serialization;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Operations.Caching;

/// <summary>
/// What is actually stored: the value, the tag versions it was written under, and when.
/// <para>
/// <b>The tag versions are how tag invalidation works without enumerating keys.</b> Invalidating a
/// tag increments one counter. Every entry carrying that tag was written under the old number, so
/// the next read compares and treats the entry as a miss. No scan, no key list, no per-tag index
/// that has to be kept correct across replicas — and the cost is one comparison per tag per read.
/// </para>
/// <para>
/// The alternative — a set of keys per tag, maintained on every write — is what most implementations
/// reach for, and it fails in the same two ways every time: the set grows without bound as entries
/// expire out from under it, and a replica that misses a write leaves a key nothing will ever drop.
/// </para>
/// </summary>
/// <param name="Value">The serialized value.</param>
/// <param name="TagVersions">Tag name to the version in force when this entry was written.</param>
/// <param name="WrittenAtTicks">UTC ticks. Used for stale-while-revalidate.</param>
internal sealed record CacheEnvelope(
    [property: JsonPropertyName("v")] JsonElement Value,
    [property: JsonPropertyName("t")] IReadOnlyDictionary<string, long>? TagVersions,
    [property: JsonPropertyName("w")] long WrittenAtTicks);

/// <summary>
/// Turns values into bytes and back.
/// <para>
/// One serializer for every tier, so an entry written by the in-memory store during a test is
/// byte-identical to one written by a shared store in production. Two formats would drift, and the
/// drift would surface only after a deployment switched stores.
/// </para>
/// </summary>
internal static class CacheSerializer
{
    /// <summary>
    /// Deliberately not <see cref="JsonSerializerOptions.Web"/>.
    /// <para>
    /// A cache round-trips its own values, so the naming policy that matters at the API boundary is
    /// noise here — and case-insensitive matching, which Web enables, is a real cost on a hot read
    /// path. Property names go to the wire exactly as declared.
    /// </para>
    /// <para>
    /// The strongly-typed-id converter is registered because cached values routinely contain them,
    /// and without it a <c>UserId</c> serializes as an object with a <c>Value</c> property and comes
    /// back as one — silently, and only in whatever code path reads the cache.
    /// </para>
    /// </summary>
    private static readonly JsonSerializerOptions Options = Create();

    public static ReadOnlyMemory<byte> Serialize<T>(T value, IReadOnlyDictionary<string, long>? tagVersions, long writtenAtTicks)
    {
        var payload = JsonSerializer.SerializeToElement(value, Options);
        var envelope = new CacheEnvelope(payload, tagVersions, writtenAtTicks);

        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static CacheEnvelope? ReadEnvelope(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<CacheEnvelope>(bytes.Span, Options);
        }
        catch (JsonException)
        {
            // A payload this process cannot read is a payload written by a different version of the
            // platform. Treating it as a miss is the only safe answer: the alternative is failing a
            // request over a cache entry, during a rolling deployment, on whichever replica upgraded
            // second.
            return null;
        }
    }

    public static T? ReadValue<T>(CacheEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            return envelope.Value.Deserialize<T>(Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());

        // populateMissingResolver: true is not optional. The parameterless overload throws when no
        // TypeInfoResolver has been set, and this options instance is a static readonly field — so the
        // throw happens in a type initializer, on the first cache write, and every subsequent touch of
        // the type raises TypeInitializationException.
        //
        // B17 shipped the parameterless call and its own tests caught it, but only because a test
        // policy set FailOnCacheError: the cache swallows store faults by design, so the platform's
        // whole cache was failing every read and write while reporting nothing but a rising error
        // count. That counter and the cache health check are what make this visible in production.
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
