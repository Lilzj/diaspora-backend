using System.ComponentModel.DataAnnotations;

namespace Diaspora.Api.RateLimiting;

/// <summary>
/// Transport-level throttling for the unauthenticated attack surface.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Turns the limiter off. Exists for the integration suite, which drives dozens of
    /// sign-ins from one address in a second and is not the traffic this defends against.
    /// The startup validator refuses to boot a non-Development host with it disabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Credential exchange: sign-in and OTP redemption.</summary>
    public RateLimitPolicyOptions SignIn { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Account creation.</summary>
    public RateLimitPolicyOptions Registration { get; set; } = new() { PermitLimit = 5, WindowSeconds = 300 };

    /// <summary>
    /// Anything that causes an SMS. Tightest of the set, because every request past the
    /// limit is money as well as noise.
    /// </summary>
    public RateLimitPolicyOptions OneTimeCode { get; set; } = new() { PermitLimit = 5, WindowSeconds = 300 };

    /// <summary>Account recovery.</summary>
    public RateLimitPolicyOptions Recovery { get; set; } = new() { PermitLimit = 5, WindowSeconds = 300 };

    /// <summary>
    /// Token rotation. Looser than the rest: a legitimate client with several tabs or a
    /// backgrounded app genuinely does refresh in bursts.
    /// </summary>
    public RateLimitPolicyOptions Refresh { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>
    /// Per-subject budgets, applied by handlers on top of the per-IP limits above. These
    /// are what a distributed attack runs into.
    /// </summary>
    public SubjectRateLimitOptions Subject { get; set; } = new();
}

public sealed class RateLimitPolicyOptions
{
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Requests parked rather than refused when the limit is hit. Zero by design: queueing
    /// an attacker holds a server thread on their behalf, and a legitimate client would
    /// rather have a 429 with a Retry-After than a hung request.
    /// </summary>
    [Range(0, 1_000)]
    public int QueueLimit { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}

public sealed class SubjectRateLimitOptions
{
    /// <summary>Sign-in attempts against one phone number, from anywhere.</summary>
    [Range(1, 1_000)]
    public int SignInPerPhone { get; set; } = 10;

    [Range(1, 86_400)]
    public int SignInWindowSeconds { get; set; } = 900;

    /// <summary>
    /// One-time codes sent to one phone number, from anywhere. Deliberately close to the
    /// per-user budget the OTP service already enforces: this one also catches requests for
    /// numbers that are not registered, which never reach that budget because there is no
    /// account to charge them to.
    /// </summary>
    [Range(1, 1_000)]
    public int OneTimeCodePerPhone { get; set; } = 5;

    [Range(1, 86_400)]
    public int OneTimeCodeWindowSeconds { get; set; } = 3_600;

    public TimeSpan SignInWindow => TimeSpan.FromSeconds(SignInWindowSeconds);

    public TimeSpan OneTimeCodeWindow => TimeSpan.FromSeconds(OneTimeCodeWindowSeconds);
}
