using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Policies;

/// <summary>
/// How aggressively repeated failed sign-ins are throttled. Passed into the
/// aggregate rather than read from configuration inside it, so the domain stays
/// free of infrastructure and the policy is trivially varied in tests.
/// </summary>
public sealed class LockoutPolicy : ValueObject
{
    public static readonly LockoutPolicy Default = new(5, TimeSpan.FromMinutes(15));

    public int MaxFailedAttempts { get; }
    public TimeSpan LockoutDuration { get; }

    private LockoutPolicy(int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        MaxFailedAttempts = maxFailedAttempts;
        LockoutDuration = lockoutDuration;
    }

    public static LockoutPolicy Create(int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        if (maxFailedAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFailedAttempts), maxFailedAttempts,
                "At least one failed attempt must be permitted before lockout.");
        if (lockoutDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration), lockoutDuration,
                "Lockout duration must be positive.");

        return new LockoutPolicy(maxFailedAttempts, lockoutDuration);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MaxFailedAttempts;
        yield return LockoutDuration;
    }
}
