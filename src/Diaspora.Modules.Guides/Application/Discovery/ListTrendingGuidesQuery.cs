using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// What is earning attention now, as opposed to what earned it once.
/// <para>
/// The score is the registered trending strategy's, never this query's: one decay curve, called
/// rather than copied, because two implementations would disagree at the margin and the margin
/// is exactly where a trending list is decided.
/// </para>
/// </summary>
public sealed record ListTrendingGuidesQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    public string? CountryCode { get; init; }

    /// <summary>Narrows the shelf to one category. Null trends across the catalogue.</summary>
    public string? CategoryKey { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
