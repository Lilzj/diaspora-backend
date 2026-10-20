namespace Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A community's initiatives — 21 Oct 2026.
//
// Every field is nullable so a missing one reaches the domain and is refused there with a sentence,
// rather than at model binding with a 400 nobody can read. No amount, goal sum or payment field
// exists on any of them: fundraising is a campaign's.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A new initiative.</summary>
public sealed record CreateCommunityInitiativeRequest
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    /// <summary>When the community hopes to be done, as a date (<c>2026-12-31</c>). Optional.</summary>
    public DateOnly? TargetDate { get; init; }

    /// <summary>Planned (the default) or Active.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// The members leading it, by user id. Omitted or empty, the creator organises it alone. Each must
    /// be a current member whose membership is visible to the community.
    /// </summary>
    public IReadOnlyList<Guid>? OrganiserIds { get; init; }
}

/// <summary>
/// A change to an initiative. <b>Partial:</b> an omitted field keeps its stored value, so an editor
/// that only moves the status cannot blank the description by not sending it.
/// </summary>
public sealed record UpdateCommunityInitiativeRequest
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public DateOnly? TargetDate { get; init; }

    /// <summary>True removes the target date. Not together with <see cref="TargetDate"/>.</summary>
    public bool? ClearTargetDate { get; init; }

    /// <summary>Planned → Active | Cancelled; Active → Completed | Cancelled.</summary>
    public string? Status { get; init; }

    /// <summary>Replaces the organisers. Only somebody with canManageInitiatives may send it.</summary>
    public IReadOnlyList<Guid>? OrganiserIds { get; init; }
}

/// <summary>A progress update, as an organiser writes it.</summary>
public sealed record PostCommunityInitiativeUpdateRequest
{
    public string? Title { get; init; }

    public string? Body { get; init; }
}
