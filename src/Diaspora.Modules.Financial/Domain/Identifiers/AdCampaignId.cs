using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>
/// An advertising campaign.
/// <para>
/// Deliberately not <c>CampaignId</c>, which this module already has and which identifies a
/// <see cref="Advertising.AdCampaign"/>&apos;s entirely unrelated namesake — a member-run
/// fundraising programme. Two identifiers, two entities, one noun.
/// </para>
/// </summary>
public sealed record AdCampaignId(Guid Value) : StronglyTypedId(Value)
{
    public static AdCampaignId New() => new(Guid.NewGuid());

    public static AdCampaignId From(Guid value) => new(value);
}
