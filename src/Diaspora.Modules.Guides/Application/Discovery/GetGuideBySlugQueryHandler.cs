using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves the guide detail screen from a deep link.
/// <para>
/// A slug that is not even well formed resolves as "not found" rather than as a validation
/// error: a bad address in somebody's chat message is a dead link, and telling its recipient
/// that their URL failed a regular expression helps nobody.
/// </para>
/// </summary>
public sealed class GetGuideBySlugQueryHandler
    : IQueryHandler<GetGuideBySlugQuery, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideReadComposer _composer;

    public GetGuideBySlugQueryHandler(
        IGuideRepository guides,
        GuideAuthorizationService authorization,
        GuideReadComposer composer)
    {
        _guides = guides;
        _authorization = authorization;
        _composer = composer;
    }

    public async Task<Result<GuideResponse>> Handle(
        GetGuideBySlugQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var slug = GuideSlug.Create(query.Slug);

        if (slug.IsFailure)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var guide = await _guides.GetBySlugAsync(slug.Value, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        return Result.Success(await _composer.ComposeAsync(guide, cancellationToken));
    }
}
