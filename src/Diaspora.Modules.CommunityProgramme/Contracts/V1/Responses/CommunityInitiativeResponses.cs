namespace Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;

/// <summary>
/// One initiative, as the caller may see it — the app's <c>CommunityInitiative</c>, 21 Oct 2026.
/// <para>
/// <b>Where it differs from the app's simulated shape.</b> <c>leads</c> is <see cref="Organisers"/>;
/// <c>startsAt</c>/<c>endsAt</c> are <see cref="StartedAt"/> (when it became Active) and
/// <see cref="TargetDate"/>; there is no <c>progress</c> figure, because nothing measures one — the
/// organisers' updates are the account of progress, counted in <see cref="UpdateCount"/>.
/// </para>
/// </summary>
public sealed record CommunityInitiativeResponse
{
    public required Guid Id { get; init; }

    public required Guid CommunityId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    /// <summary>Planned, Active, Completed or Cancelled.</summary>
    public required string Status { get; init; }

    public DateOnly? TargetDate { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>When it was completed or cancelled.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>
    /// The organisers this caller may see, each as they chose to be seen: Public to anybody who may
    /// read the community, CommunityOnly to its members. One who left the community is not listed.
    /// </summary>
    public required IReadOnlyList<CommunityRoleHolderResponse> Organisers { get; init; }

    /// <summary>Current members taking part, whether or not each is visible to this caller.</summary>
    public required int ParticipantCount { get; init; }

    public required int UpdateCount { get; init; }

    public required bool IsParticipant { get; init; }

    public required bool IsOrganiser { get; init; }

    /// <summary>Whether this caller may join now: a current member, and it is Planned or Active.</summary>
    public required bool CanJoin { get; init; }

    /// <summary>Whether this caller may change it. The server checks again on the write.</summary>
    public required bool CanEdit { get; init; }

    /// <summary>Whether this caller may post an update. The server checks again on the write.</summary>
    public required bool CanPostUpdate { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Somebody taking part, as they chose to be seen.</summary>
public sealed record CommunityInitiativeParticipantResponse
{
    public required Guid UserId { get; init; }

    public required string DisplayName { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }
}

/// <summary>Who wrote an update. Absent where their membership is not visible to this caller.</summary>
public sealed record CommunityInitiativeAuthorResponse
{
    public required Guid UserId { get; init; }

    public required string DisplayName { get; init; }
}

/// <summary>A progress update an organiser posted.</summary>
public sealed record CommunityInitiativeUpdateResponse
{
    public required Guid Id { get; init; }

    public required Guid InitiativeId { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required DateTimeOffset PostedAt { get; init; }

    public CommunityInitiativeAuthorResponse? Author { get; init; }
}
