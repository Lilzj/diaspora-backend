using Diaspora.Application.Abstractions;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Application.Activity;

/// <summary>One page of the caller's own recognised activity, newest first.</summary>
public sealed record GetMyCreatorActivityQuery : IQuery<CreatorActivityPageResponse>
{
    /// <summary>Cursor. Everything strictly older than this instant.</summary>
    public DateTimeOffset? Before { get; init; }

    public int? Take { get; init; }
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

    /// <summary>Pass back as <c>before</c>. Null when the page is the last one.</summary>
    public DateTimeOffset? NextCursor { get; init; }

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

        var take = Math.Clamp(query.Take ?? DefaultTake, 1, 100);

        // One extra row, so `hasMore` is known without a second count query.
        var rows = await _activity.ListForCreatorAsync(
            creator.Id, query.Before, take + 1, cancellationToken);

        var hasMore = rows.Count > take;
        var page = hasMore ? rows.Take(take).ToList() : rows;

        return Result.Success(new CreatorActivityPageResponse
        {
            Items = [.. page.Select(Describe)],
            NextCursor = hasMore ? page[^1].OccurredAt : null,
            HasMore = hasMore,
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
