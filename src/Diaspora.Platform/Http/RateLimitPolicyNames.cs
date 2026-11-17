namespace Diaspora.Platform.Http;

/// <summary>
/// The names of the platform's transport rate-limit policies, in the one assembly that both the
/// host and every module can see.
/// <para>
/// <b>Why they moved here.</b> They were constants on the host's <c>RateLimitingRegistration</c>,
/// which a module cannot reference — so the three module groups that wanted the authenticated
/// budget wrote the string by hand, one of them with the comment "the literal rather than the
/// host's constant: a module does not reference the host". That comment described a real
/// constraint and the wrong conclusion. A misspelled policy name is not a compile error and not a
/// startup error: ASP.NET Core raises it on the request that reaches the endpoint, so the
/// misspelling ships, and the endpoint it was meant to protect is the one that breaks.
/// </para>
/// <para>
/// <b>Naming a policy is the exception, not the rule.</b> Every endpoint composed through the host
/// is given <see cref="Anonymous"/> or <see cref="Authenticated"/> automatically, according to
/// whether it requires authentication. A module names a policy here only to ask for something
/// different from its tier's default — a search, a webhook, an upload — and that is deliberately a
/// visible edit at the route.
/// </para>
/// </summary>
public static class RateLimitPolicyNames
{
    /// <summary>Credential exchange: sign-in and one-time-code redemption.</summary>
    public const string SignIn = "diaspora-signin";

    /// <summary>Account creation.</summary>
    public const string Registration = "diaspora-registration";

    /// <summary>Anything that sends an SMS. The tightest budget on the platform.</summary>
    public const string OneTimeCode = "diaspora-otp";

    /// <summary>Account recovery, and the pages an emailed link opens.</summary>
    public const string Recovery = "diaspora-recovery";

    /// <summary>Token rotation.</summary>
    public const string Refresh = "diaspora-refresh";

    /// <summary>
    /// Authenticated application traffic. The default for every endpoint that requires
    /// authentication; sized for an application rather than for a credential guess.
    /// </summary>
    public const string Authenticated = "diaspora-authenticated";

    /// <summary>
    /// The default for every endpoint that does not require authentication — discovery,
    /// catalogues, slug lookups, the pages an email opens. Tighter than
    /// <see cref="Authenticated"/>, because nothing about the caller is known and no
    /// authorisation decision stands behind the request.
    /// </summary>
    public const string Anonymous = "diaspora-anonymous";

    /// <summary>
    /// Anonymous search. Declared at the route rather than inferred, and tighter again than
    /// <see cref="Anonymous"/>: a search accepts caller-supplied filters and page sizes and
    /// answers them from the database, so one request costs far more than a read of one row.
    /// </summary>
    public const string Search = "diaspora-search";

    /// <summary>
    /// Anonymous engagement writes: the counters a reader bumps without signing in.
    /// <para>
    /// BSEC-004. Seven routes across four commands — a guide view, a guide share, a notice
    /// activity signal and the four country knowledge signals — each of which is deliberately
    /// anonymous because a figure that counted only members would understate the one number that
    /// measures whether the knowledge base works. They feed trending, discovery and the
    /// country-level intelligence the Analytics module projects, so an unbounded one is
    /// content-ranking manipulation at no cost and with no attribution.
    /// </para>
    /// <para>
    /// <b>Tighter than <see cref="Anonymous"/> because a refusal here is cheap.</b> A 429 on a
    /// search costs the visitor the first page they asked for; a 429 on a view counter costs one
    /// analytics increment and nothing the reader can see — the guide was already served by a
    /// different request. That asymmetry is what lets this budget be strict.
    /// </para>
    /// <para>
    /// And it is why the budget is not stricter still: under carrier-grade NAT a popular guide is
    /// read by many people from one address, so an over-tight bound would make the counters
    /// systematically <i>under</i>-report. The finding is about inflation, and trading it for
    /// deflation would not be a fix.
    /// </para>
    /// </summary>
    public const string Engagement = "diaspora-engagement";

    /// <summary>
    /// Provider callbacks. Unauthenticated by nature — the signature is checked inside the
    /// handler, after the body has been read — so the budget is what bounds the cost of an
    /// unsigned request.
    /// </summary>
    public const string Webhook = "diaspora-webhook";

    /// <summary>
    /// Chunked upload traffic. Looser than <see cref="Authenticated"/> and separate from it: a
    /// large upload legitimately makes many requests, and it should not be able to spend the
    /// budget the member's screens are drawing on.
    /// </summary>
    public const string Upload = "diaspora-upload";
}
