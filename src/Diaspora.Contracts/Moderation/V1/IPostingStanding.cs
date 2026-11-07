namespace Diaspora.Contracts.Moderation.V1;

/// <summary>
/// Whether a member may post in an area right now (7 Nov 2026).
/// <para>
/// A first strike stops a member posting in the area where it happened for seven days, a second for
/// thirty. Trust holds the strikes; every module asks through this seam before letting a member create
/// or publish, so a strike in jobs blocks jobs and nothing else.
/// </para>
/// </summary>
public interface IPostingStanding
{
    /// <summary>The block in force for this member in this area, or null when they may post.</summary>
    Task<PostingBlock?> GetBlockAsync(Guid userId, string area, CancellationToken cancellationToken = default);
}

/// <summary>A member may not post in <paramref name="Area"/> until <paramref name="Until"/>.</summary>
public sealed record PostingBlock(string Area, DateTimeOffset Until, string Level);
