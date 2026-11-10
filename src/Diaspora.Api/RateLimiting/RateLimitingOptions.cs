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
    /// Authenticated application traffic, for the module groups that ask to be throttled.
    /// <para>
    /// Three hundred a minute is not a credential-exchange budget and is not meant to be.
    /// It bounds a client that has already proved who it is, where the real control is the
    /// authorisation on each endpoint; the limiter is there so a compromised or runaway
    /// client cannot drive unbounded load. Before this existed those groups borrowed
    /// <see cref="SignIn"/>, and ten requests a minute shared with <c>/auth/login</c> is a
    /// chat screen that stops loading.
    /// </para>
    /// </summary>
    public RateLimitPolicyOptions Authenticated { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>
    /// Every endpoint that does not require authentication: discovery, catalogues, slug
    /// lookups, the pages an emailed link opens.
    /// <para>
    /// Tighter than <see cref="Authenticated"/> and for a different reason. An authenticated
    /// request is attributable to a member and stands behind an authorisation decision; an
    /// anonymous one is attributable to an address, and the address is all there is. Two a
    /// second from one address is not a person reading a page.
    /// </para>
    /// <para>
    /// <b>This is not "the public tier", and the number reflects that.</b> Measured against the
    /// composed route table: 521 of the platform's 1,100 endpoints carry no route-level
    /// authorisation, and that is not an oversight — around half of the platform's writes are
    /// authorised in the CQRS pipeline instead, by <c>[Authorize]</c> on the command.
    /// <c>POST /financial/wallets/me/withdrawals</c> is one of them. So this bucket holds a
    /// signed-in member's wallet traffic alongside anonymous guide reads, and it has to be sized
    /// for the former. Twenty a minute would have been defensible for a public read surface and
    /// would have broken the wallet.
    /// <para>
    /// The tightening that matters is therefore <see cref="Search"/>, declared at the route,
    /// where the surface really is anonymous and really is expensive.
    /// </para>
    /// </para>
    /// <para>
    /// <b>The limit of a per-address budget, stated rather than implied.</b> The limiter runs
    /// before authentication, so the address is the only dimension available — and behind
    /// carrier-grade NAT a great many people share one. A budget that distinguishes people rather
    /// than addresses needs an identity the request does not yet carry; that is
    /// <c>ISubjectRateLimiter</c>'s job, inside the handlers, and it is a different control rather
    /// than a better version of this one.
    /// </para>
    /// </summary>
    public RateLimitPolicyOptions Anonymous { get; set; } = new() { PermitLimit = 240, WindowSeconds = 60 };

    /// <summary>
    /// Anonymous search, declared at the route.
    /// <para>
    /// A search and a read are different questions. <c>POST /api/v1/jobs/search</c> and
    /// <c>GET /api/v1/guides</c> take filters and a page size from the caller and answer them
    /// from the database; one request is a query plan, not a row. On a scale-to-zero compute
    /// that makes an unbounded anonymous search a cost event as much as an availability one.
    /// </para>
    /// <para>
    /// <b>Sixty rather than the thirty this was first set to, and the reason is the same
    /// carrier-NAT argument that sets <see cref="Anonymous"/>.</b> This budget belongs to an
    /// address, and the discovery surface is the one every anonymous visitor arrives on — on a
    /// mobile network a great many of them arrive from the same address. Thirty a minute is one
    /// request every two seconds for an entire carrier pool, which is a 429 on the first page a
    /// visitor sees.
    /// <para>
    /// Sixty is still a bound worth having: one request a second of filtered search from a single
    /// address is survivable load, against no bound at all before this. It is a judgement and not
    /// a measurement, which is worth saying plainly — the number to revisit first once there is
    /// real traffic to measure, and the reason to prefer a per-member budget over a per-address
    /// one as soon as the request carries an identity.
    /// </para>
    /// </para>
    /// </summary>
    public RateLimitPolicyOptions Search { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>
    /// Provider callbacks.
    /// <para>
    /// These are the platform's only endpoints that are unauthenticated <i>by design</i> and
    /// expensive <i>before</i> they can be refused: each reads up to a megabyte and runs
    /// HMAC-SHA256 over the body once per configured secret before deciding the body was not
    /// signed. Loose enough for a real redelivery burst, which arrives in a batch rather than
    /// at a trickle.
    /// </para>
    /// </summary>
    public RateLimitPolicyOptions Webhook { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    /// <summary>
    /// Chunked upload traffic.
    /// <para>
    /// Deliberately looser than <see cref="Authenticated"/>, and the point is that it is
    /// <i>separate</i>. A hundred-megabyte file is twenty legitimate requests; metered against
    /// the member's application budget, one upload would stop their screens loading. A bucket
    /// of its own bounds upload abuse without that side effect.
    /// </para>
    /// </summary>
    public RateLimitPolicyOptions Upload { get; set; } = new() { PermitLimit = 600, WindowSeconds = 60 };

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
