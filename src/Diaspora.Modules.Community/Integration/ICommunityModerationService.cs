using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// Reporting and moderation, for a module that owns content.
/// <para>
/// A module calling this gets the report modal's whole workflow — the reason catalogue, the
/// duplicate rule, the append-only decision history — without implementing any of it. That is the
/// alternative to what would otherwise happen: four verticals with four report tables and four
/// subtly different definitions of resolved.
/// </para>
/// <para>
/// Every report filed here also opens or joins the Trust moderation case for that content
/// (7 Nov 2026), under the content type the owning module's <c>IModerationTarget</c> answers to.
/// Staff decide there, and the owning module carries the decision out.
/// </para>
/// </summary>
public interface ICommunityModerationService
{
    Task<Result<ContentReport>> ReportAsync(
        ContentReference content,
        UserId reporterId,
        ReportReasonCode reason,
        string? description = null,
        string? evidenceUrl = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModerationDecision>> GetHistoryAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    Task<int> CountOpenReportsAsync(
        ContentReference content, CancellationToken cancellationToken = default);
}
