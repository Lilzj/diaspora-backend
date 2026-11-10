using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Interviews;

/// <summary>
/// The interview rounds arranged against one application.
/// <para>
/// **Gap G-3, closed.** `POST /applications/{id}/interviews` has existed since the hiring wave and
/// there was no way to read the result: the employer dashboard reported
/// <c>upcomingInterviewCount</c> and offered nothing to open, and a candidate who had been invited
/// to an interview could not see when it was. Runtime evidence in
/// `runtime-remediation/e2e-completion/jobs-e2e.md` — the write returns 201 and every read of the
/// same resource answered 404 or 405.
/// </para>
/// <para>
/// Both sides of the table may read it, because both are participants in the same conversation —
/// the employer's team through membership, the candidate through owning the application. The
/// projection differs by <c>forEmployer</c>, which is the flag <see cref="Mapping.InterviewMapper"/>
/// already takes.
/// </para>
/// </summary>
[Authorize(Policy = JobPolicies.ApplicationsReview)]
public sealed record ListApplicationInterviewsQuery : IQuery<IReadOnlyList<InterviewResponse>>
{
    public required Guid ApplicationId { get; init; }
}
