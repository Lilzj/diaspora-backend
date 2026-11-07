using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Answers "may this caller do this to this provider?".
/// <para>
/// <b>One implementation, used by every write handler.</b> Thirty-odd commands mutate a provider,
/// and each of them needs the same three-line check: is there a caller, do they hold a role,
/// is that role high enough. Written thirty times it would be written twenty-nine times
/// correctly, and the thirtieth would be an editor who can transfer ownership.
/// </para>
/// <para>
/// <b>Staff never pass the owner check (7 Nov 2026).</b> A moderator or an administrator who is
/// not a member of a provider cannot edit its profile, contacts, prices, photos, services or
/// hours, or move its status, through the owner's routes. Staff act on a provider only through
/// a decision on its Trust moderation case to suspend or reinstate it, with a reason and a note,
/// and <c>PUT /providers/{providerId}/featured</c> for the carousel. Before this,
/// <see cref="RequireRole"/> began with "if the caller is staff, allow", so any moderator could
/// rewrite a member's business with no report, no reason and no word to the owner.
/// </para>
/// <para>
/// Staff <em>reading</em> is unchanged: <see cref="CanSeeUnpublished"/> still shows them
/// drafts, suspended and archived providers so they can review them.
/// </para>
/// </summary>
public sealed class ProviderAuthorizationService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;

    public ProviderAuthorizationService(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>The caller, or null when the request is anonymous.</summary>
    public UserId? CallerId => _currentUser.UserId;

    /// <summary>Whether the caller holds a platform staff role.</summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(ProviderErrors.NotAuthenticated);

    /// <summary>
    /// Confirms the caller holds at least a role on the provider.
    /// <para>
    /// Returns the caller's identifier on success, so a handler that needs both — which is
    /// nearly all of them, for the event payload — makes one call rather than two.
    /// </para>
    /// <para>
    /// No staff bypass: staff are refused here like any other non-member. See the class summary.
    /// </para>
    /// </summary>
    public Result<UserId> RequireRole(Provider provider, ProviderRole minimumRole)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        return provider.HasAtLeast(caller.Value, minimumRole)
            ? caller
            : Result.Failure<UserId>(ProviderErrors.NotOwned);
    }

    /// <summary>Whether the caller may read a provider that is not publicly discoverable.</summary>
    public bool CanSeeUnpublished(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return IsStaff || provider.HasAtLeast(_currentUser.UserId, ProviderRole.Contributor);
    }

    /// <summary>
    /// The audiences this caller is eligible for, for a discovery query.
    /// <para>
    /// Computed here and put into the filter rather than evaluated per row, which is what keeps
    /// visibility a <c>WHERE</c> clause instead of a post-filter that pages wrongly — a page of
    /// twenty that drops six to visibility returns fourteen, and the client's infinite scroll
    /// silently shortens.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<ProviderVisibilityLevel> AllowedVisibility()
    {
        if (_currentUser.UserId is null)
        {
            return [ProviderVisibilityLevel.Public];
        }

        // A signed-in member sees public and members-only. Country- and area-scoped providers
        // are included because the scope is checked against the member's own geography by the
        // query, not by this list; excluding them here would hide a Ghanaian caterer from the
        // Ghanaian community it chose to serve.
        return
        [
            ProviderVisibilityLevel.Public,
            ProviderVisibilityLevel.Members,
            ProviderVisibilityLevel.CountryCommunity,
            ProviderVisibilityLevel.AreaOnly
        ];
    }
}
