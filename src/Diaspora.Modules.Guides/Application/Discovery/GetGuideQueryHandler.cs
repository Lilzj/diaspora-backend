using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves the guide detail screen.
/// <para>
/// <b>A guide the caller may not see returns "not found", not "forbidden".</b> The difference
/// matters: a 403 confirms that a guide with that identifier exists, which is exactly what
/// somebody probing an unlisted draft wants to learn. A 404 tells them nothing.
/// </para>
/// <para>
/// Everything cross-cutting is hydrated by <see cref="GuideReadComposer"/>, shared with the slug
/// read: provider cards from B7's directory, the author's standing from this module's Trust
/// projection, their name and photo, Community's authoritative comment count, and the reader's
/// own save and vote. Each fails soft on its own — a directory or comments outage renders a
/// slightly thinner page, never a failed one.
/// </para>
/// <para>
/// <b>The read records nothing.</b> A view is a fact about a reader and belongs to
/// <c>RecordGuideViewCommand</c>; a query that quietly incremented a counter would make every
/// cache warm-up and every crawler look like readership.
/// </para>
/// </summary>
public sealed class GetGuideQueryHandler : IQueryHandler<GetGuideQuery, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideReadComposer _composer;

    public GetGuideQueryHandler(
        IGuideRepository guides,
        GuideAuthorizationService authorization,
        GuideReadComposer composer)
    {
        _guides = guides;
        _authorization = authorization;
        _composer = composer;
    }

    public async Task<Result<GuideResponse>> Handle(
        GetGuideQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guide = await _guides.GetForReadAsync(GuideId.From(query.GuideId), cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        return Result.Success(await _composer.ComposeAsync(guide, cancellationToken));
    }
}
