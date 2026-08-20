using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Hashtags;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Hashtags;

/// <summary>
/// Serves the trending list.
/// <para>
/// Reads a stored window rather than aggregating live. Counting uses across every post on the
/// platform on each render of a discover screen is the query that takes a social database down.
/// </para>
/// </summary>
public sealed class ListTrendingHashtagsQueryHandler
    : IQueryHandler<ListTrendingHashtagsQuery, IReadOnlyList<HashtagResponse>>
{
    private readonly IHashtagRepository _hashtags;

    public ListTrendingHashtagsQueryHandler(IHashtagRepository hashtags) => _hashtags = hashtags;

    public async Task<Result<IReadOnlyList<HashtagResponse>>> Handle(
        ListTrendingHashtagsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trending = await _hashtags.ListTrendingAsync(
            request.CountryCode?.ToUpperInvariant(),
            Math.Clamp(request.Limit, 1, 50),
            cancellationToken);

        return Result.Success<IReadOnlyList<HashtagResponse>>(
            trending.Select(HashtagMapper.ToResponse).ToList());
    }
}
