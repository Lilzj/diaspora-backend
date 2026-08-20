using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Hashtags;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Hashtags;

/// <summary>Suggests tags. Suppressed and blocked tags never appear.</summary>
public sealed class SuggestHashtagsQueryHandler
    : IQueryHandler<SuggestHashtagsQuery, IReadOnlyList<HashtagResponse>>
{
    private readonly IHashtagRepository _hashtags;

    public SuggestHashtagsQueryHandler(IHashtagRepository hashtags) => _hashtags = hashtags;

    public async Task<Result<IReadOnlyList<HashtagResponse>>> Handle(
        SuggestHashtagsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var suggestions = await _hashtags.SuggestAsync(
            HashtagSlug.Normalise(request.Prefix),
            Math.Clamp(request.Limit, 1, 25),
            cancellationToken);

        return Result.Success<IReadOnlyList<HashtagResponse>>(
            suggestions.Select(HashtagMapper.ToResponse).ToList());
    }
}
