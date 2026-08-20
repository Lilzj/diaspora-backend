using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Services;

/// <summary>
/// Answers "may this caller edit official information?".
/// <para>
/// <b>The same shape as B8.1's, and simpler than B8's for the same reason.</b> A guide has an author,
/// so every write against one asks a second question after the permission check: is this yours. An
/// official notice has no author — it has an editorial desk, and everything on that desk is visible
/// to everybody on it. There is deliberately no <c>RequireOwner</c> here to be tempted into writing:
/// an intelligence team where one member cannot correct another's transcription of a circular during
/// a cyclone is a team that will publish the wrong thing.
/// </para>
/// <para>
/// <b><see cref="IsVerifier"/> is narrower than <see cref="IsEditor"/>, and that is the phase's only
/// separation of duty.</b> Anybody on the desk can write a notice; confirming that an authority
/// really said it is a distinct grant, because a workflow where the person who transcribes also
/// approves is a verification step that verifies nothing.
/// </para>
/// <para>
/// The endpoints are already gated by policy before a handler runs. This exists for the handlers that
/// need the caller's identifier for an event payload, and for the reads that show an editor
/// unpublished state a reader must not see.
/// </para>
/// </summary>
public sealed class IntelligenceAuthorizationService
{
    private static readonly HashSet<string> EditorRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private static readonly HashSet<string> VerifierRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;

    public IntelligenceAuthorizationService(ICurrentUser currentUser) =>
        _currentUser = currentUser;

    /// <summary>The caller, or null when the request is anonymous. Reads are anonymous by design.</summary>
    public UserId? CallerId => _currentUser.UserId;

    /// <summary>
    /// Whether the caller may see drafts, submissions and archived notices.
    /// <para>
    /// Role-scoped rather than tier-scoped, per B2.0.1: a tier measures progression and a role grants
    /// privilege. No amount of verifying your phone number makes you an intelligence editor, and it
    /// should not — this is authority over what the platform presents as government information.
    /// </para>
    /// </summary>
    public bool IsEditor => _currentUser.Role is { } role && EditorRoles.Contains(role);

    /// <summary>Whether the caller may confirm that an authority really issued something.</summary>
    public bool IsVerifier => _currentUser.Role is { } role && VerifierRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every editing handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(IntelligenceErrors.NotAuthenticated);
}
