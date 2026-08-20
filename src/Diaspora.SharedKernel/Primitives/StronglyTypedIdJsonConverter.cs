using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// JSON converter for a specific strongly typed ID.
/// </summary>
public sealed class StronglyTypedIdJsonConverter<TId> : JsonConverter<TId>
    where TId : StronglyTypedId
{
    public override TId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetGuid();
        return (TId?)Activator.CreateInstance(typeToConvert, value);
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
