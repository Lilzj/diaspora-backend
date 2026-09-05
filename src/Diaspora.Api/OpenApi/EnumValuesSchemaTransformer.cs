using System.Text.Json.Nodes;
using Diaspora.SharedKernel.Contracts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Diaspora.Api.OpenApi;

/// <summary>
/// Writes the values named by <see cref="OpenApiEnumValuesAttribute"/> into the schema.
/// </summary>
/// <remarks>
/// The attribute lives in <c>Diaspora.SharedKernel</c> so the modules can apply it; this half stays
/// here, because it is the only project that takes a dependency on Microsoft.OpenApi.
/// </remarks>
public sealed class EnumValuesSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        var attribute = context.JsonPropertyInfo?
            .AttributeProvider?
            .GetCustomAttributes(typeof(OpenApiEnumValuesAttribute), inherit: true)
            .OfType<OpenApiEnumValuesAttribute>()
            .FirstOrDefault();

        if (attribute is null)
        {
            return Task.CompletedTask;
        }

        // Microsoft.OpenApi v2 carries enum values as `JsonNode`, not the v1 `IOpenApiAny`.
        schema.Enum =
        [
            .. Enum.GetNames(attribute.EnumType).Select(name => (JsonNode)JsonValue.Create(name))
        ];

        return Task.CompletedTask;
    }
}
