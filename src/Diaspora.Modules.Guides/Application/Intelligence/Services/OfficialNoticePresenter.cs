using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Guides.Application.Intelligence.Services;

/// <summary>
/// Turns one notice into a response, resolving what the mapper needs.
/// <para>
/// <b>Ten command handlers return a notice, and without this every one of them would repeat four
/// lines of resolution.</b> Repetition on that scale is not a style problem: it is ten places for the
/// eleventh handler to be written slightly differently, and the difference that eventually appears is
/// a card missing its authority name on one screen and not another.
/// </para>
/// <para>
/// Editors always get the editor view from a command handler — somebody who just wrote a notice needs
/// the revision back to make their next edit, and the verification note is the reason a rejection
/// happened.
/// </para>
/// </summary>
public sealed class OfficialNoticePresenter
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IClock _clock;

    public OfficialNoticePresenter(
        INoticeCategoryRepository categories,
        IIssuingAuthorityRepository authorities,
        IClock clock)
    {
        _categories = categories;
        _authorities = authorities;
        _clock = clock;
    }

    /// <summary>The full notice, resolved and mapped.</summary>
    public async Task<OfficialNoticeResponse> PresentAsync(
        OfficialNotice notice,
        bool forEditor,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);

        // Including archived on both, because a notice can outlive the category it was filed under
        // and the ministry that issued it — and a live notice rendered with a blank source because
        // the ministry was restructured is worse than one naming a body under its old name.
        var category = await _categories.GetIncludingArchivedAsync(
            notice.CategoryKey, cancellationToken);

        var authority = await _authorities.GetIncludingArchivedAsync(
            notice.AuthorityId, cancellationToken);

        return OfficialNoticeMapper.ToResponse(
            notice, category, authority, _clock.UtcNow, locale, forEditor);
    }
}
