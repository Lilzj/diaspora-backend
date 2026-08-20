using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Services;

/// <summary>
/// Who is asking, and what they are allowed to ask about.
/// <para>
/// <b>Every member-facing handler in this module starts with <see cref="RequireCaller"/>, and none of
/// them takes a user identifier from the request.</b> That is what makes reading somebody else's
/// recommendations, profile or preferences unexpressible rather than merely forbidden — there is no
/// parameter to put another member's identity into.
/// </para>
/// <para>
/// <b>The tier is resolved from claims rather than by asking Identity.</b> This module holds no
/// dependency on Identity and does not want one; the kernel puts the tier on the caller's claims and
/// B2.0.1 fixed what the ladder means, so reading it here is reading a platform contract rather than
/// reaching into a module.
/// </para>
/// </summary>
public sealed class RecommendationAuthorizationService
{
    private readonly ICurrentUser _currentUser;

    public RecommendationAuthorizationService(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>
    /// The caller's identity, or a failure.
    /// <para>
    /// A <c>Result</c> rather than an exception because "not signed in" is an ordinary outcome on
    /// these endpoints — a token expires mid-scroll — and the API's problem document is a better
    /// answer than a stack trace.
    /// </para>
    /// </summary>
    public Result<UserId> RequireCaller()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
        {
            return Result.Failure<UserId>(Error.Unauthorized(
                RecommendationErrorCodes.NotAuthenticated,
                "Recommendations are personal. Sign in to see yours."));
        }

        return Result.Success(userId);
    }

    /// <summary>
    /// The caller's access tier, from their claims.
    /// <para>
    /// Falls back to <see cref="AccessTier.Anonymous"/> on anything unparseable, which is the
    /// conservative direction: the access-tier policy shows an anonymous caller public content and
    /// nothing else, so a claim this module cannot read costs a member some recommendations rather
    /// than leaking any.
    /// </para>
    /// </summary>
    public AccessTier ResolveTier()
    {
        if (!_currentUser.IsAuthenticated)
        {
            return AccessTier.Anonymous;
        }

        var claim = _currentUser.Claims.GetValueOrDefault(PlatformClaimTypes.AccessTier);

        return Enum.TryParse<AccessTier>(claim, ignoreCase: true, out var tier)
            ? tier

            // Authenticated but with no readable tier claim. Limited is what an onboarded member has
            // and is the floor at which the platform is browsable, so this degrades to "signed in"
            // rather than to "signed out".
            : AccessTier.Limited;
    }

    /// <summary>Whether the caller holds a permission. Used by the administration handlers.</summary>
    public bool HasPermission(string permission) =>
        _currentUser.Permissions.Contains(permission, StringComparer.Ordinal);

    /// <summary>
    /// Parses a surface name from a route or a query string.
    /// <para>
    /// A failure rather than a default, because a client asking for a surface this module does not
    /// have is a client with a bug, and silently answering with the home feed would hide it.
    /// </para>
    /// </summary>
    public static Result<RecommendationSurface> ParseSurface(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success(RecommendationSurface.HomeFeed);
        }

        return Enum.TryParse<RecommendationSurface>(value.Trim(), ignoreCase: true, out var surface)
            ? Result.Success(surface)
            : Result.Failure<RecommendationSurface>(Error.Validation(
                RecommendationErrorCodes.SurfaceUnknown,
                $"'{value}' is not a recommendation surface."));
    }

    /// <summary>Parses a target type. A failure rather than a default, for the same reason.</summary>
    public static Result<RecommendationTargetType> ParseTargetType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<RecommendationTargetType>(Error.Validation(
                RecommendationErrorCodes.TargetTypeUnknown, "A target type is required."));
        }

        return Enum.TryParse<RecommendationTargetType>(value.Trim(), ignoreCase: true, out var type)
            ? Result.Success(type)
            : Result.Failure<RecommendationTargetType>(Error.Validation(
                RecommendationErrorCodes.TargetTypeUnknown,
                $"'{value}' is not a recommendation target type."));
    }
}
