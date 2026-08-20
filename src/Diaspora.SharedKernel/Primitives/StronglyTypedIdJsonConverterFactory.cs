using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// JSON converter factory for strongly typed IDs.
/// Automatically handles serialization/deserialization of all StronglyTypedId derivatives.
/// </summary>
public sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsAssignableTo(typeof(StronglyTypedId));
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(StronglyTypedIdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}
