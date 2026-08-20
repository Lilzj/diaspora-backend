using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Discovery;

/// <summary>
/// One job in full.
/// <para>
/// <b>Carries no authorisation attribute, and that is the module's whole point.</b> Published
/// vacancies are public: a member looking for work must be able to read what work exists before
/// anybody asks them to prove who they are. Visibility is applied inside the handler — a draft, a
/// private posting and a hidden one resolve as <em>not found</em> to everyone but the owning
/// employer and staff, because a 403 would confirm the posting exists.
/// </para>
/// </summary>
public sealed record GetJobQuery : IQuery<JobResponse>
{
    public Guid? JobId { get; init; }

    /// <summary>The deep-link handle. Either this or the identifier.</summary>
    public string? Slug { get; init; }
}
