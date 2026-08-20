using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Policies;

/// <summary>
/// The lifetime, retry and resend budget applied to a one-time code.
/// </summary>
public sealed class OtpPolicy : ValueObject
{
    public static readonly OtpPolicy Default = new(
        TimeSpan.FromMinutes(10),
        maxVerificationAttempts: 5,
        maxResends: 3,
        resendCooldown: TimeSpan.FromSeconds(60));

    public TimeSpan Lifetime { get; }
    public int MaxVerificationAttempts { get; }
    public int MaxResends { get; }
    public TimeSpan ResendCooldown { get; }

    private OtpPolicy(TimeSpan lifetime, int maxVerificationAttempts, int maxResends, TimeSpan resendCooldown)
    {
        Lifetime = lifetime;
        MaxVerificationAttempts = maxVerificationAttempts;
        MaxResends = maxResends;
        ResendCooldown = resendCooldown;
    }

    public static OtpPolicy Create(
        TimeSpan lifetime,
        int maxVerificationAttempts,
        int maxResends,
        TimeSpan resendCooldown)
    {
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Code lifetime must be positive.");
        if (maxVerificationAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxVerificationAttempts), maxVerificationAttempts,
                "At least one verification attempt must be permitted.");
        if (maxResends < 0)
            throw new ArgumentOutOfRangeException(nameof(maxResends), maxResends, "Resend budget cannot be negative.");
        if (resendCooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(resendCooldown), resendCooldown,
                "Resend cooldown cannot be negative.");

        return new OtpPolicy(lifetime, maxVerificationAttempts, maxResends, resendCooldown);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Lifetime;
        yield return MaxVerificationAttempts;
        yield return MaxResends;
        yield return ResendCooldown;
    }
}
