using Diaspora.Application.Abstractions;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Application.Activity;

/// <summary>One page of the caller's own recognised activity, newest first.</summary>
/// <remarks>
/// <b>This attribute was missing, and its absence made the route anonymous</b> (BSEC-003, the one
/// instance the pre-launch audit confirmed live). The endpoint carries no
/// <c>RequireAuthorization</c> — no route in <c>Diaspora.Modules.Creator/Api</c> does, the module
/// authorises entirely through this attribute — so with nothing here, the only thing between
/// <c>GET /api/v1/creators/me/activity</c> and an unauthenticated caller was the handler's own
/// <c>RequireUser()</c>. That is defence in depth doing its job, not authorisation.
/// <para>
/// <c>ProfileRead</c> rather than <c>Participate</c>, which the audit suggested: every sibling
/// caller-scoped read takes <c>ProfileRead</c> — earnings, revenue, reputation, milestones,
/// referral programme, referral history — and <c>Participate</c> is what the module requires of an
/// <i>act</i>. Reading one's own timeline is not an act, and <c>Participate</c> would have made
/// this read stricter than the earnings beside it.
/// </para>
/// </remarks>
[RequirePermission(CreatorPermissions.ProfileRead)]
public sealed record GetMyCreatorActivityQuery : IQuery<CreatorActivityPageResponse>
{
    /// <summary>
    /// The opaque cursor from the previous page's <c>nextCursor</c>, or null for the first page.
    /// </summary>
    /// <remarks>
    /// <b>Named for what the client sends.</b> The route bound <c>before</c> and <c>take</c>; the
    /// client has always sent <c>cursor</c> and <c>limit</c>. Neither bound, so the cursor was
    /// discarded on every request and the second page was the first one again — for as long as the
    /// route has existed. Query binding has no opinion about a parameter nobody sends.
    /// </remarks>
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

/// <summary>One recognised action, as the workspace timeline renders it.</summary>
public sealed record CreatorActivityEntryResponse
{
    public required string ActionType { get; init; }

    public required int Weight { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}

public sealed record CreatorActivityPageResponse
{
    public required IReadOnlyList<CreatorActivityEntryResponse> Items { get; init; }

    /// <summary>
    /// Pass back as <c>cursor</c>. Null when the page is the last one.
    /// <para>
    /// Opaque, and a string because it names a row rather than a moment — see
    /// <c>CreatorActivityCursor</c>. The client already treated it as one it handed back
    /// untouched, which is what let the position gain its second component without the client
    /// changing at all.
    /// </para>
    /// </summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>
    /// Whether the caller is a creator at all.
    /// <para>
    /// False and an empty page is *"you have not started publishing"*; true and an empty page is
    /// *"nothing recognised yet"*. An empty list alone cannot separate them, and a screen that
    /// rendered "no activity" at somebody who has never published would be answering a question
    /// they did not ask.
    /// </para>
    /// </summary>
    public required bool IsCreator { get; init; }

    public static CreatorActivityPageResponse NotACreator() => new()
    {
        Items = [],
        NextCursor = null,
        HasMore = false,
        IsCreator = false
    };
}

/// <summary>
/// Reads a creator's own activity history.
/// <para>
/// <b>The gap this closes.</b> `CreatorActivityEntry` has been written on every recognised action
/// since the module shipped — a publish, a milestone, a referral — and
/// <c>ICreatorActivityRepository</c> could count them, count one action type and rank a
/// leaderboard from them, but had no method that handed the rows back. There was no
/// <c>GET /creators/me/activity</c>, so the workspace carried *"Diaspora cannot show your activity
/// history yet. The platform records what you publish, but serves no route to read that history
/// back"* — which was exactly right, and is now exactly wrong.
/// </para>
/// <para>
/// <b>Nothing is derived here.</b> The response carries the action type, the weight, the subject
/// reference and the instant, because those are the columns. It does not carry a human sentence
/// for each action: naming what "guide.published" means to a member is a presentation decision,
/// the client already owns that vocabulary in `TimelineItem`, and a second one written server-side
/// would drift from it.
/// </para>
/// </summary>
public sealed class GetMyCreatorActivityQueryHandler
    : IQueryHandler<GetMyCreatorActivityQuery, CreatorActivityPageResponse>
{
    private const int DefaultTake = 25;

    private readonly CreatorContext _context;
    private readonly ICreatorActivityRepository _activity;

    public GetMyCreatorActivityQueryHandler(
        CreatorContext context, ICreatorActivityRepository activity)
    {
        _context = context;
        _activity = activity;
    }

    public async Task<Result<CreatorActivityPageResponse>> Handle(
        GetMyCreatorActivityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorActivityPageResponse>(caller.Error);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        // Not a 404. Having published nothing is an ordinary state, and answering "not found"
        // would make the workspace render an error at somebody who has simply not started.
        if (creator is null)
        {
            return Result.Success(CreatorActivityPageResponse.NotACreator());
        }

        // The token is opaque to this handler: the repository issues it and reads it back,
        // the same way every other paged read in this module does. A malformed one serves the
        // first page rather than a 400 — ADR-017's trade, because a stale token is an ordinary
        // event (an old app version, a shared link) and a browse screen answering 400 is worse
        // than one answering rows already seen.
        var page = await _activity.ListForCreatorAsync(
            creator.Id, query.Cursor, query.Limit ?? DefaultTake, cancellationToken);

        return Result.Success(new CreatorActivityPageResponse
        {
            Items = [.. page.Entries.Select(Describe)],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            IsCreator = true
        });
    }

    private static CreatorActivityEntryResponse Describe(CreatorActivityEntry entry) => new()
    {
        ActionType = entry.ActionType,
        Weight = entry.Weight,
        SubjectType = entry.SubjectType,
        SubjectId = entry.SubjectId,
        CountryCode = entry.CountryCode,
        OccurredAt = entry.OccurredAt
    };
}
