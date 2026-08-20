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
    /// A reader matches a neighbourhood audience if they are in that area or anywhere beneath
    /// it. The prefix match is on Location's own materialised path, copied onto the content —
    /// B6 compares two strings and does no hierarchy reasoning of its own.
    /// </summary>
    private static bool MatchesNeighborhood(VisibilityRequest request)
    {
        var targetArea = request.Audience.AreaId ?? request.ContentAreaId;

        if (targetArea is null)
        {
            return true;
        }

        if (request.ViewerAreaId == targetArea)
        {
            return true;
        }

        return request.ContentHierarchyPath is { } contentPath
               && request.ViewerHierarchyPath is { } viewerPath
               && viewerPath.StartsWith(contentPath, StringComparison.Ordinal);
    }
}
