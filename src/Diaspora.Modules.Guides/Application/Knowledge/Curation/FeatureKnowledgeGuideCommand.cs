using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>
/// Puts a guide on one country's curated shelf.
/// <para>
/// <b>The command the brief calls "Feature Guide", and it is not the one B8 already has.</b> B8's
/// featured flag is global: a featured guide is featured everywhere, which is right for a
/// platform-wide carousel and wrong for a knowledge directory, where the first thing to read about
/// Mauritius and the first thing to read about Canada are different guides and one boolean cannot
/// hold both.
/// </para>
/// <para>
/// <b>It does not touch the guide.</b> The placement lives on the country directory, so featuring
/// neither loads the guide for writing, nor bumps its revision, nor gives its author an edit
/// conflict caused by somebody else's curation.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.Curate)]
[Auditable]
public sealed record FeatureKnowledgeGuideCommand : ICommand
{
    public required string CountryCode { get; init; }

    public required FeatureKnowledgeGuideRequest Request { get; init; }
}
