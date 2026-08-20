using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one image, video or document in a provider's gallery.</summary>
public sealed record ProviderGalleryItemId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderGalleryItemId New() => new(Guid.NewGuid());

    public static ProviderGalleryItemId From(Guid value) => new(value);
}
