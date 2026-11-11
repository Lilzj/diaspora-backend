using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Catalog;

/// <summary>
/// The employment-domain catalogue.
/// <para>
/// Unauthenticated: a member has to be able to see what kinds of work exist before they can
/// browse for any of it.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListJobCategoriesQuery : IQuery<IReadOnlyList<JobCategoryResponse>>
{
    /// <summary>Retired categories too. Staff surfaces only; the default is the live set.</summary>
    public bool IncludeInactive { get; init; }
}
