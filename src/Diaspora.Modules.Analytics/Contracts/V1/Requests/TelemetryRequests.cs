using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diaspora.Modules.Analytics.Contracts.V1.Requests;

/// <summary>
/// Reads a telemetry property bag whose values are scalars of any JSON type.
/// <para>
/// <b>B24. Without this the endpoint refuses the client's own payload, and refuses the whole batch
/// with it.</b> The client's sanitiser accepts <c>string | number | boolean</c> and sends each as its
/// natural JSON type — <c>{ "actionCount": 3, "wasSimulated": true }</c> — while the property bag is
/// declared as <c>Dictionary&lt;string, string?&gt;</c>, which <c>System.Text.Json</c> refuses to
/// populate from a number or a boolean. The result was a 400 on the whole request rather than a
/// per-event verdict, for every event carrying a count or a flag.
/// </para>
/// <para>
/// <b>This predates B24 and was unreachable until it registered an event that carries one.</b> The
/// existing allow-list already had <c>position</c> and <c>isSimulated</c>; no registered event
/// carried either, so nothing had ever sent one. <c>automation.tested</c> carries both an
/// <c>actionCount</c> and a <c>wasSimulated</c>, which is what made a latent contract mismatch a
/// live one.
/// </para>
/// <para>
/// <b>Objects and arrays are still refused, deliberately.</b> The client refuses them at emit, and a
/// nested object is the usual way a whole DTO with an email address in it reaches a telemetry
/// payload. A refusal here is a malformed request — the request's shape is wrong, not one event's
/// content — which is the distinction the per-event reason codes rest on.
/// </para>
/// </summary>
public sealed class TelemetryPropertyBagConverter : JsonConverter<Dictionary<string, string?>>
{
    public override Dictionary<string, string?> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A telemetry property bag must be an object.");
        }

        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return properties;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("A telemetry property bag must be a flat object.");
            }

            var key = reader.GetString()!;

            reader.Read();

            properties[key] = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Null => null,
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",

                // The number's own text, not a round trip through a numeric type. Reading it as a
                // `double` would render 3 as "3" on one culture and turn a large identifier-shaped
                // number into exponent notation on any of them.
                JsonTokenType.Number => JsonElement.ParseValue(ref reader).GetRawText(),

                _ => throw new JsonException(
                    $"Telemetry property '{key}' is not a scalar. Objects and arrays are refused.")
            };
        }

        throw new JsonException("A telemetry property bag ended unexpectedly.");
    }

    /// <summary>
    /// Writes the bag back out as strings.
    /// <para>
    /// Never used by the API — this type appears on a request and nothing serialises one — and
    /// implemented rather than thrown from because a converter that throws on write turns any
    /// diagnostic dump of a request into a second failure.
    /// </para>
    /// </summary>
    public override void Write(
        Utf8JsonWriter writer, Dictionary<string, string?> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        foreach (var (key, item) in value)
        {
            writer.WriteString(key, item);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// One event a client reports about its own use of the application.
/// <para>
/// <b>The shape the client has emitted into a buffer since Phase 7</b> — <c>event</c>,
/// <c>properties</c>, <c>occurredAt</c> — and deliberately nothing more. The register's own words:
/// "<c>deliver()</c> currently pushes to a buffer that nothing reads. Nothing else in the app
/// changes when it gains a destination."
/// </para>
/// <para>
/// <b>There is no user identifier here and there must never be one.</b> The client's property
/// allow-list has none, the server attributes every row to the authenticated caller, and a field
/// added here would make the client's four-phase-old sanitiser advisory.
/// </para>
/// </summary>
public sealed record TelemetryEventRequest
{
    /// <summary>A registered event name. An unregistered one is refused with a named code.</summary>
    public string? Event { get; init; }

    /// <summary>
    /// Which schema version this was emitted against. Defaults to 1.
    /// <para>
    /// Optional so the existing client — which sends no version — keeps working, and present so a
    /// second version can ship without the first becoming ambiguous.
    /// </para>
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    /// When it happened, set at emit.
    /// <para>
    /// Not when it was delivered. A buffered event that kept only its delivery time would put a
    /// member's whole offline session into the minute they came back online.
    /// </para>
    /// </summary>
    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>
    /// The allow-listed properties.
    /// <para>
    /// <b>Scalars only, and the server checks rather than trusts.</b> The client refuses objects and
    /// arrays at emit; an ingestion endpoint that accepted them would make that refusal advisory,
    /// and a nested object is the usual way a whole DTO with an email address in it reaches a
    /// telemetry payload.
    /// </para>
    /// <para>
    /// <b>Read through <see cref="TelemetryPropertyBagConverter"/>, and B24 added it because the
    /// declared type was refusing the client's own payload.</b> A count arrives as a JSON number and
    /// a flag as a JSON boolean; a <c>Dictionary&lt;string, string?&gt;</c> populated by the default
    /// converter refuses both and fails the whole batch. Every value is stored as its text, which is
    /// what the allow-list check, the length cap and the fact's own columns all work in.
    /// </para>
    /// </summary>
    [JsonConverter(typeof(TelemetryPropertyBagConverter))]
    public Dictionary<string, string?>? Properties { get; init; }
}

/// <summary>
/// A batch of telemetry.
/// <para>
/// Batched because the client buffers, and capped because an uncapped write path open to every
/// device is a denial-of-service surface with a friendly name.
/// </para>
/// </summary>
public sealed record TelemetryBatchRequest
{
    public IReadOnlyList<TelemetryEventRequest>? Events { get; init; }
}

/// <summary>An administrative request to rebuild a bounded window of rollups.</summary>
public sealed record AnalyticsBackfillRequest
{
    /// <summary>Inclusive, <c>YYYY-MM-DD</c>.</summary>
    public string? From { get; init; }

    /// <summary>Inclusive, <c>YYYY-MM-DD</c>.</summary>
    public string? To { get; init; }
}
