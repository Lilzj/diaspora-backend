using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>
/// Publishes a posting.
/// <para>
/// The single most consequential command in the module: it puts a vacancy into the search index,
/// fires every matching standing alert, and starts the clock the employer's response-time
/// reputation is measured against.
/// </para>
/// </summary>
[RequirePermission(JobPermissions.PostPublish)]
[Auditable]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Jobs)]
public sealed record PublishJobCommand : ICommand<JobResponse>
{
    public required Guid JobId { get; init; }

    public required PublishJobRequest Request { get; init; }
}
