using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A buyer/seller conversation reference's identity.</summary>
public sealed record ListingConversationId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingConversationId New() => new(Guid.NewGuid());

    public static ListingConversationId From(Guid value) => new(value);
}
