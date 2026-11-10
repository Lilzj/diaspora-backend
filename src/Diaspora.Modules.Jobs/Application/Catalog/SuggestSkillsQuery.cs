using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Catalog;

/// <summary>
/// Skill autocomplete, or the popular set when no term is given.
/// <para>
/// This is what stops "React", "ReactJS" and "react.js" becoming three skills in the first
/// place. Unauthenticated, because it powers the search box.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record SuggestSkillsQuery : IQuery<IReadOnlyList<JobSkillCatalogResponse>>
{
    /// <summary>A prefix. Omitted, the most-used skills come back instead.</summary>
    public string? Term { get; init; }

    /// <summary>Narrow to one employment domain.</summary>
    public string? CategoryKey { get; init; }

    public int Limit { get; init; } = 20;
}
