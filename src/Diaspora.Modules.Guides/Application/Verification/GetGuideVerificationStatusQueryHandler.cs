using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Verification;

/// <summary>
/// Serves the author's verification console.
/// <para>
/// Read straight off the guide's trust mark, which is a <em>projection</em> of Trust's decision —
/// nothing in this module can move a mark to verified, and this handler asks Trust nothing. That
/// is what makes the read cheap enough to poll while a claim is pending.
/// </para>
/// <para>
/// Author or staff only, enforced here as well as by the permission: the permission says the
/// caller may deal with verification at all, and this says whose guide it is.
/// </para>
/// </summary>
public sealed class GetGuideVerificationStatusQueryHandler
    : IQueryHandler<GetGuideVerificationStatusQuery, GuideVerificationStatusResponse>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;

    public GetGuideVerificationStatusQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization)
    {
        _guides = guides;
        _authorization = authorization;
    }

    public async Task<Result<GuideVerificationStatusResponse>> Handle(
        GetGuideVerificationStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guide = await _guides.GetForReadAsync(GuideId.From(query.GuideId), cancellationToken);

        if (guide is null || guide.IsDeleted)
        {
            return Result.Failure<GuideVerificationStatusResponse>(GuideErrors.NotFound);
        }

        var author = _authorization.RequireAuthorOrStaffToRead(guide);

        if (author.IsFailure)
        {
            return Result.Failure<GuideVerificationStatusResponse>(author.Error);
        }

        var mark = guide.TrustMark;

        return Result.Success(new GuideVerificationStatusResponse
        {
            GuideId = guide.Id.Value,
            State = GuideWireNames.ToWire(mark.State),
            VerificationId = mark.VerificationId,
            Badge = mark.Badge,
            Reason = mark.Reason,
            VerifiedAt = mark.VerifiedAt,
            ChangedAt = mark.ChangedAt
        });
    }
}
