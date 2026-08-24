using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>A landlord review's identity.</summary>
public sealed record LandlordReviewId(Guid Value) : StronglyTypedId(Value)
{
    public static LandlordReviewId New() => new(Guid.NewGuid());

    public static LandlordReviewId From(Guid value) => new(value);
}
