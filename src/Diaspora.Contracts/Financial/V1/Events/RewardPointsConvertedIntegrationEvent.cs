using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// Points became money.
/// <para>
/// <b>The one crossing on the platform, and the only event that carries both units.</b> Everything
/// upstream of it counts points; everything downstream of it counts minor units. Publishing the rate
/// that was used alongside both figures is what makes a conversion auditable a year later without
/// re-deriving it from a policy version that has since moved on.
/// </para>
/// </summary>
public sealed record RewardPointsConvertedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversionId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required long Points { get; init; }

    public required long CreditedMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>Minor units per point, as applied. Never recomputed from today's policy.</summary>
    public required decimal RateMinorUnitsPerPoint { get; init; }

    /// <summary>The policy version that decided the rate. <c>reward.conversion@2</c>.</summary>
    public string? PolicyVersion { get; init; }

    public required Guid WalletId { get; init; }

    public required DateTimeOffset ConvertedAt { get; init; }
}
