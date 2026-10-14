using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Extensibility.Visibility;

/// <summary>
/// The built-in evaluator: the audience the author declared.
/// <para>
/// Handles every level B6 models, and abstains on <see cref="VisibilityLevel.Custom"/> — a
/// stored rule is evaluated by the rule evaluator, and an evaluator that guessed at a rule it
/// could not read would be guessing about who may read something.
/// </para>
/// <para>
/// Two checks come before the audience and override it. A blocked reader never sees anything,
/// whatever the audience says. An author always sees their own content, including their own
/// drafts and their own hidden posts — a member who cannot see their own removed post cannot
/// appeal it.
/// </para>
/// </summary>
public sealed class AudienceVisibilityEvaluator : IVisibilityEvaluator
{
    /// <summary>
    /// How near a reader's active location must be to a neighbourhood-only post's pin, in
    /// kilometres (14 Oct 2026).
    /// </summary>
    public const double NeighborhoodRadiusKm = Domain.Policies.CommunityLimits.NeighborhoodAudienceRadiusKm;

    public int Order => 0;

    public VisibilityDecision Evaluate(VisibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsBlocked)
        {
            return VisibilityDecision.Deny("blocked");
        }

        if (request.ViewerIsAuthor)
        {
            return VisibilityDecision.Allow;
        }

        return request.Audience.Level switch
        {
            VisibilityLevel.Public => VisibilityDecision.Allow,

            VisibilityLevel.Members => request.ViewerId is null
                ? VisibilityDecision.Deny("members-only")
                : VisibilityDecision.Allow,

            VisibilityLevel.VerifiedMembers => request.ViewerIsVerified
                ? VisibilityDecision.Allow
                : VisibilityDecision.Deny("verified-only"),

            VisibilityLevel.Country => MatchesCountry(request)
                ? VisibilityDecision.Allow
                : VisibilityDecision.Deny("country-mismatch"),

            VisibilityLevel.Neighborhood => MatchesNeighborhood(request)
                ? VisibilityDecision.Allow
                : VisibilityDecision.Deny("area-mismatch"),

            VisibilityLevel.Followers => request.ViewerFollowsAuthor
                ? VisibilityDecision.Allow
                : VisibilityDecision.Deny("followers-only"),

            VisibilityLevel.Private => VisibilityDecision.Deny("private"),

            // A stored rule is somebody else's to read.
            VisibilityLevel.Custom => VisibilityDecision.Abstain,

            _ => VisibilityDecision.Deny("unknown-audience")
        };
    }

    /// <summary>
    /// A reader matches a country audience if they are currently in it or Trust has verified
    /// them as a resident of it. Both, because a diaspora member visiting home is still of that
    /// country, and one abroad is still a resident of where they live.
    /// </summary>
    private static bool MatchesCountry(VisibilityRequest request)
    {
        var target = request.Audience.CountryCode ?? request.ContentCountryCode;

        if (target is null)
        {
            return true;
        }

        return string.Equals(request.ViewerCountryCode, target, StringComparison.OrdinalIgnoreCase)
               || request.ViewerVerifiedCountries.Contains(target, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A reader matches a neighbourhood audience when their active location is within
    /// <see cref="NeighborhoodRadiusKm"/> of the content's pin.
    /// <para>
    /// <b>A radius around the post's point since 14 Oct 2026</b>, not a catalogue area: the platform
    /// is retiring its place catalogue, and an area id matched nobody — no reader's context had
    /// carried one since the catalogue stopped being written. The distance is measured by Location
    /// and handed in; B6 computes no geometry of its own.
    /// </para>
    /// <para>
    /// <b>Content with no pin falls back to its country</b>, matched the way a country audience is.
    /// Content with neither is readable by any signed-in member — the same widening to "members"
    /// a provider's area-only visibility makes when its scope cannot be enforced — never by a
    /// signed-out reader. A reader with no active location sees no pinned neighbourhood content.
    /// </para>
    /// </summary>
    private static bool MatchesNeighborhood(VisibilityRequest request)
    {
        if (request.ViewerId is null)
        {
            return false;
        }

        if (request.ContentHasPoint)
        {
            return request.ViewerDistanceKm is { } distance && distance <= NeighborhoodRadiusKm;
        }

        var country = request.ContentCountryCode ?? request.Audience.CountryCode;

        if (country is null)
        {
            return true;
        }

        return string.Equals(request.ViewerCountryCode, country, StringComparison.OrdinalIgnoreCase)
               || request.ViewerVerifiedCountries.Contains(country, StringComparer.OrdinalIgnoreCase);
    }
}
