using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>
/// Drafts a job posting.
/// <para>
/// A draft, never a live vacancy. Postings are written over sessions and usually reviewed by
/// somebody else before they go out; publishing straight from creation would put half-written
/// adverts in front of people deciding where to spend an afternoon.
/// </para>
/// <para>
/// <c>[Transactional]</c>, because the handler writes two aggregates: the posting and its default
/// hiring pipeline. A posting whose pipeline failed to save would be one no application could
/// ever enter.
/// </para>
/// </summary>
[Authorize(Permission = JobPermissions.PostCreate)]
[Auditable]
[Transactional(Reason = "Writes the posting and its default hiring pipeline together.")]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Jobs)]
public sealed record CreateJobCommand : ICommand<JobResponse>
{
    public required CreateJobRequest Request { get; init; }
}
