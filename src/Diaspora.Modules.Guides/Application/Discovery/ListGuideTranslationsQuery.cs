using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// The other language editions of one guide.
/// <para>
/// What the language switcher on a guide page is built from. Returned as a plain list rather
/// than a page: a translation group is one guide per language, so it is small by construction
/// and a cursor over it would be ceremony.
/// </para>
/// </summary>
public sealed record ListGuideTranslationsQuery : IQuery<IReadOnlyList<GuideCardResponse>>
{
    public required Guid GuideId { get; init; }
}
