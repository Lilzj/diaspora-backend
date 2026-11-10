using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>
/// Corrects the language a guide is written in.
/// <para>
/// A correction, not a translation: the content does not change, only the claim about what
/// language it is in. It matters because the language decides which member of a translation
/// group a reader is offered, and a guide mislabelled at creation is offered to the wrong people
/// until somebody fixes it.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.EditOwn)]
[Auditable]
public sealed record SetGuideLanguageCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required SetGuideLanguageRequest Request { get; init; }
}
