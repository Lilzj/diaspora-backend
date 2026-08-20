using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Answers "may this caller curate?".
/// <para>
/// <b>Much simpler than B8's equivalent, and the reason is worth stating.</b> A guide has an author,
/// so every write against one asks a second question after the permission check: is this yours. A
/// country directory has no author. It is platform furniture, curated by staff, and the permission
/// catalogue is the whole of the answer — which is why this service has three members where B8's has
/// six, and why there is no <c>RequireOwner</c> here to be tempted into writing.
/// </para>
/// <para>
/// The endpoints are already gated by policy before a handler runs. This exists for the handlers
/// that need the caller's identifier for an event payload, and for the reads that show a curator
/// unpublished state a reader must not see.
/// </para>
/// </summary>
public sealed class KnowledgeAuthorizationService
{
    private static readonly HashSet<string> CuratorRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;

    public KnowledgeAuthorizationService(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>The caller, or null when the request is anonymous. Reads are anonymous by design.</summary>
    public UserId? CallerId => _currentUser.UserId;

    /// <summary>
    /// Whether the caller may see unpublished directories, hidden rows and draft paths.
    /// <para>
    /// Role-scoped rather than tier-scoped, per B2.0.1: a tier measures progression and a role
    /// grants privilege. No amount of verifying your phone number makes you a knowledge curator.
    /// </para>
    /// </summary>
    public bool IsCurator => _currentUser.Role is { } role && CuratorRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every curation handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(KnowledgeErrors.NotAuthenticated);
}
