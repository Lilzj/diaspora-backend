using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// What was published most recently.
/// <para>
/// The only ordering with no judgement in it, and the only one that gives a guide published this
/// morning any chance of being read at all — every other shelf rewards accumulated evidence, and
/// a knowledge base where accumulated evidence is the only currency never lets anything new
/// accumulate any.
/// </para>
/// </summary>
public sealed record ListLatestGuidesQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    /// <summary>Narrows to one country. Null lists everywhere.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Narrows to one language. Null lists every language.</summary>
    public string? Language { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
