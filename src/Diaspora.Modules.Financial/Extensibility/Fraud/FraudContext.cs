using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Fraud;

/// <summary>What a fraud assessor is told about a payment before it is attempted.</summary>
public sealed record FraudContext
{
    public required Guid PayerUserId { get; init; }

    public required Money Amount { get; init; }

    public required PaymentMethodKind MethodKind { get; init; }

    public SubjectReference? Subject { get; init; }

    public string? CountryCode { get; init; }

    public string? IpAddress { get; init; }

    public string? DeviceId { get; init; }

    /// <summary>How many payments this member has attempted in the last hour.</summary>
    public int RecentAttemptCount { get; init; }

    /// <summary>How much this member has successfully paid in the last day, in minor units.</summary>
    public long RecentVolumeMinorUnits { get; init; }

    /// <summary>Whether the member holds a Trust verification. Projected, never queried.</summary>
    public bool IsVerified { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}
