using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>
/// Changes who can see the caller's profile.
/// <para>
/// <b>Its own command rather than a field on the profile save, deliberately.</b> This is the one
/// setting a candidate changes urgently and on its own — somebody who has just realised their
/// employer can find them wants a single tap, not a form that also rewrites their work history
/// and can fail validation on an unrelated field.
/// </para>
/// </summary>
[Authorize(Permission = JobPermissions.CandidateManage)]
[Auditable]
public sealed record ChangeCandidateVisibilityCommand : ICommand
{
    /// <summary><c>Private</c>, <c>VerifiedEmployers</c>, <c>Members</c> or <c>Public</c>.</summary>
    public required string Visibility { get; init; }
}
