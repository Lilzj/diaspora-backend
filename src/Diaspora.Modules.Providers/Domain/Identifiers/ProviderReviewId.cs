using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one member's rated review of a provider.</summary>
public sealed record ProviderReviewId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderReviewId New() => new(Guid.NewGuid());

    public static ProviderReviewId From(Guid value) => new(value);
}
