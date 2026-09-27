namespace Diaspora.Modules.GeographicIntelligence.Domain.Routing;

/// <summary>
/// What happened when a routing provider was asked.
/// <para>
/// <b>Seven outcomes rather than a boolean, because the phase brief's §40 is right that they are
/// not the same fact.</b> A vendor nobody contracted, a vendor that is down, a vendor refusing the
/// quota and a genuine absence of any road between two points all end with no route on screen, and
/// they call for four different things: a product decision, an alert, a backoff, and a message to
/// the member. Collapsing them is how a client ends up retrying a strait.
/// </para>
/// <para>
/// None of them is ever converted into a fabricated travel time. That is the one rule this whole
/// seam exists to keep.
/// </para>
/// </summary>
public enum RouteOutcome
{
    /// <summary>The provider planned the journey.</summary>
    Success = 0,

    /// <summary>
    /// No routing vendor is contracted for this deployment.
    /// <para>
    /// The state this platform ships in. It is a configuration fact, not a failure, and a client
    /// reading it should stop offering the capability rather than retry it.
    /// </para>
    /// </summary>
    NotConfigured = 1,

    /// <summary>A provider is configured and could not be reached, or timed out.</summary>
    Unavailable = 2,

    /// <summary>
    /// The provider is configured and reachable but does not answer this travel mode — or does not
    /// cover this city. Transit is the usual reason: coverage in London is universal and coverage
    /// where much of this community lives is the question that decides a vendor.
    /// </summary>
    Unsupported = 3,

    /// <summary>The provider refused for quota. Distinct from unavailable because it recovers.</summary>
    RateLimited = 4,

    /// <summary>The provider rejected the request as malformed. A defect here, not there.</summary>
    InvalidRequest = 5,

    /// <summary>
    /// The provider answered, and the answer is that no route exists. An island, a closed border,
    /// a pedestrian origin inside a motorway junction. Not a failure and never retryable.
    /// </summary>
    NoRoute = 6,

    /// <summary>
    /// The deployment's monthly allowance with its provider is spent. Distinct from
    /// <see cref="RateLimited"/>, which recovers in moments: this recovers next month, and telling a
    /// member to try again shortly would be untrue.
    /// </summary>
    AllowanceExhausted = 7
}
