using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Modules.Providers.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Reviews;

/// <summary>
/// Writes a review, or revises the caller's existing one.
/// <para>
/// <b>Writing twice edits rather than failing.</b> The database enforces one review per member
/// per provider, and a client that resubmits — a double tap, a retry after a timeout — would
/// otherwise get a 409 for doing the obvious thing. Routing the second write to a revision is
/// what the member meant, and the edit window still applies.
/// </para>
/// <para>
/// <b>A member attached to the provider may not review it.</b> Not the owner, not a manager, not
/// an editor. Self-reviews are the cheapest way to poison a directory's ratings and the hardest
/// to detect afterwards.
/// </para>
/// <para>
/// The rollup is recomputed from the reviews table before the response is built, so the caller
/// sees the rating their own review produced rather than the one from a moment ago.
/// </para>
/// </summary>
public sealed class WriteProviderReviewCommandHandler
    : ICommandHandler<WriteProviderReviewCommand, ProviderReviewResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly IProviderReviewRepository _reviews;
    private readonly ReviewSummaryService _summaries;
    private readonly ProviderAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public WriteProviderReviewCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        IProviderReviewRepository reviews,
        ReviewSummaryService summaries,
        ProviderAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _reviews = reviews;
        _summaries = summaries;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ProviderReviewResponse>> Handle(
        WriteProviderReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderReviewResponse>(caller.Error);
        }

        var providerId = ProviderId.From(command.ProviderId);
        var provider = await _providers.GetAsync(providerId, cancellationToken);

        if (provider is null || provider.IsDeleted)
        {
            return Result.Failure<ProviderReviewResponse>(ProviderErrors.NotFound);
        }

        // Anybody attached to the provider, at any role. An "editor" reviewing the business they
        // help run is the same conflict as the owner doing it.
        if (provider.RoleOf(caller.Value) is not null)
        {
            return Result.Failure<ProviderReviewResponse>(ProviderErrors.ReviewSelf);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var existing = await _reviews.FindByReviewerAsync(providerId, caller.Value, cancellationToken);

        var review = existing is null
            ? await CreateAsync(providerId, caller.Value, command, now, cancellationToken)
            : Revise(existing, command, now);

        if (review.IsFailure)
        {
            return Result.Failure<ProviderReviewResponse>(review.Error);
        }

        // Saved before the rollup is recomputed, and again after. The order is not stylistic:
        // ReviewSummaryService recomputes from a GROUP BY over the reviews table, and a review
        // still sitting in the change tracker is not in that table yet. Refreshing first would
        // recompute the summary that existed a moment ago — leaving the provider showing "no
        // reviews" on the very request that added one, and drifting further with every edit.
        //
        // Both saves are inside the command's transaction, so either everything lands or nothing
        // does.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _summaries.RefreshAsync(provider, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ProviderTelemetry.ReviewsSubmitted.Add(
            1,
            new KeyValuePair<string, object?>(
                "rating", request.Rating.ToString(CultureInfo.InvariantCulture)));

        return Result.Success(ProviderReviewMapper.ToResponse(review.Value));
    }

    private async Task<Result<ProviderReview>> CreateAsync(
        ProviderId providerId,
        SharedKernel.Identity.UserId reviewerId,
        WriteProviderReviewCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var review = ProviderReview.Write(
            ProviderReviewId.From(_guids.NewSequentialGuid()),
            providerId,
            reviewerId,
            command.Request.Rating,
            now,
            command.Request.Title,
            command.Request.Body,
            command.Request.ServiceId is { } serviceId
                ? ProviderServiceId.From(serviceId)
                : null);

        if (review.IsFailure)
        {
            return review;
        }

        await _reviews.AddAsync(review.Value, cancellationToken);

        return review;
    }

    private static Result<ProviderReview> Revise(
        ProviderReview existing, WriteProviderReviewCommand command, DateTimeOffset now)
    {
        var revised = existing.Revise(
            command.Request.Rating, command.Request.Title, command.Request.Body, now);

        return revised.IsFailure
            ? Result.Failure<ProviderReview>(revised.Error)
            : Result.Success(existing);
    }
}
