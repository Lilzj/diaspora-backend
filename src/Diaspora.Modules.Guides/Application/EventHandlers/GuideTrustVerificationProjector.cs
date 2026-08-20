using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Infrastructure.Trust;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Applies a Trust decision to whichever guide raised the claim.
/// <para>
/// <b>The only path to a verified mark in this module.</b> Nothing an author, a curator or a
/// staff member can do through B8's API reaches <c>Guide.ApplyVerificationDecision</c>; the four
/// Trust event handlers call this, and nothing else does. That is ADR-002's separation applied to
/// a badge readers will act on — a verified guide about a visa deadline is advice somebody books
/// a flight around.
/// </para>
/// <para>
/// <b>Matching is by Trust's own verification identifier</b>, which B8 stored when it submitted.
/// The member on the event is not enough: an author may hold several claims, and only one of them
/// is this case. Correlating by identifier also works for a claim this deployment never saw
/// submitted — after a replay, or across a deployment gap.
/// </para>
/// <para>
/// <b>Every write is guarded on the event's timestamp</b>, inside the aggregate: delivery is
/// at-least-once and unordered, so a redelivered approval from last week must not re-verify a
/// guide Trust revoked yesterday. <c>Guide.ApplyVerificationDecision</c> drops a stale decision
/// silently and reports success, because from the handler's point of view the message <em>was</em>
/// processed correctly.
/// </para>
/// <para>
/// A decision about a verification type that has nothing to do with guides — phone possession,
/// business registration, diaspora eligibility — returns silently. That is the common case in a
/// healthy system, not an error.
/// </para>
/// </summary>
public sealed class GuideTrustVerificationProjector
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GuideTrustVerificationProjector> _logger;

    public GuideTrustVerificationProjector(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        ILogger<GuideTrustVerificationProjector> logger)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ApplyAsync(
        Guid verificationId,
        string verificationType,
        GuideVerificationState outcome,
        DateTimeOffset decidedAt,
        string? badge = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!GuideVerificationTypeMap.IsGuideVerification(verificationType))
        {
            return;
        }

        var guide = await _guides.GetByVerificationIdAsync(verificationId, cancellationToken);

        if (guide is null)
        {
            // Trust decided a guide verification and B8 holds no claim for it. Worth a warning:
            // it means either a submission this module never recorded, or a guide hard-deleted
            // between submission and decision.
            _logger.LogWarning(
                "Trust verification {VerificationId} ({Type}) matched no guide claim.",
                verificationId,
                verificationType);

            return;
        }

        var applied = guide.ApplyVerificationDecision(
            outcome, verificationId, badge, reason, decidedAt);

        if (applied.IsFailure)
        {
            _logger.LogWarning(
                "Applying Trust verification {VerificationId} to guide {GuideId} failed: {Error}.",
                verificationId,
                guide.Id.Value,
                applied.Error.Code);

            return;
        }

        // Saved here, deliberately. An integration event handler runs out of band, in its own
        // scope, outside any command's unit of work — nothing else is going to commit this.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
