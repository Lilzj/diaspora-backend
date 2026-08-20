using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>
/// The record that one settled payment bought one community membership.
/// <para>
/// A join and nothing more. The money is the payment intent's, the membership is B18's, and this
/// identifies the recognition of one by the other.
/// </para>
/// </summary>
public sealed record MembershipPaymentBindingId(Guid Value) : StronglyTypedId(Value)
{
    public static MembershipPaymentBindingId New() => new(Guid.NewGuid());

    public static MembershipPaymentBindingId From(Guid value) => new(value);
}

/// <summary>A member-run fundraising programme, and the lifecycle around a giving cause.</summary>
public sealed record CampaignId(Guid Value) : StronglyTypedId(Value)
{
    public static CampaignId New() => new(Guid.NewGuid());

    public static CampaignId From(Guid value) => new(value);
}

/// <summary>One published post on a campaign's own timeline.</summary>
public sealed record CampaignUpdateId(Guid Value) : StronglyTypedId(Value)
{
    public static CampaignUpdateId New() => new(Guid.NewGuid());

    public static CampaignUpdateId From(Guid value) => new(value);
}

/// <summary>A standing instruction to give the same amount to the same cause again.</summary>
public sealed record RecurringDonationId(Guid Value) : StronglyTypedId(Value)
{
    public static RecurringDonationId New() => new(Guid.NewGuid());

    public static RecurringDonationId From(Guid value) => new(value);
}
