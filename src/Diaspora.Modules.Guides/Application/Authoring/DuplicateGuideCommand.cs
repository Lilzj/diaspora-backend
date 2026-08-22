using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Copies a guide the caller owns into a new draft — BG-07.
/// <para>
/// <b>Why this exists.</b> A "Duplicate" control sat in Creator Studio's quick-actions sheet across
/// several phases and did nothing, because no module on the platform had a clone route — the client
/// eventually said so in plain language rather than offering a button that failed. The reconciliation
/// programme's BG-07 was the decision behind that sentence, and the decision taken was to build it.
/// </para>
/// <para>
/// <b>What a duplicate is, precisely.</b> A new <em>draft</em>, authored by the caller, carrying the
/// source's editorial content and none of its history. That distinction is the whole design and
/// every field below is on one side of it or the other.
/// </para>
/// </summary>
public sealed record DuplicateGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    /// <summary>
    /// An optional title for the copy. Null takes the source's, prefixed — see the handler.
    /// </summary>
    public string? Title { get; init; }
}
