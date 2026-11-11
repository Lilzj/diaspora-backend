using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Recommendations.V1.Creator;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Recommendations.Application.Creator;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-013 — GET /recommendations/creator and its dismissal.
//
// This module owns the URL space and the Creator Platform owns the knowledge, so the handlers here
// do three things only: identify the caller, ask the provider, and commit. No candidate is ranked
// by this module's engine — the creator rail is a list of facts about the caller, not a feed.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The caller's creator suggestions, ordered by the server.</summary>
[RequirePermission(RecommendationPermissions.FeedRead)]
public sealed record GetCreatorRecommendationsQuery : IQuery<IReadOnlyList<CreatorRecommendationResponse>>
{
    public int? Take { get; init; }
}

public sealed class GetCreatorRecommendationsQueryHandler
    : IQueryHandler<GetCreatorRecommendationsQuery, IReadOnlyList<CreatorRecommendationResponse>>
{
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IEnumerable<ICreatorRecommendationProvider> _providers;

    public GetCreatorRecommendationsQueryHandler(
        RecommendationAuthorizationService authorization,
        IEnumerable<ICreatorRecommendationProvider> providers)
    {
        _authorization = authorization;
        _providers = providers;
    }

    public async Task<Result<IReadOnlyList<CreatorRecommendationResponse>>> Handle(
        GetCreatorRecommendationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CreatorRecommendationResponse>>(caller.Error);
        }

        // No provider means the Creator Platform is not composed into this host. Nothing to
        // suggest is the honest answer to that, not an error the client would have to explain.
        var provider = _providers.FirstOrDefault();

        if (provider is null)
        {
            return Result.Success<IReadOnlyList<CreatorRecommendationResponse>>([]);
        }

        var take = query.Take is null or <= 0
            ? CreatorRecommendationLimits.DefaultTake
            : Math.Min(query.Take.Value, CreatorRecommendationLimits.MaxTake);

        var items = await provider.ListAsync(caller.Value.Value, take, cancellationToken);

        return Result.Success<IReadOnlyList<CreatorRecommendationResponse>>(
            items.Select(item => new CreatorRecommendationResponse
                {
                    RecommendationId = item.RecommendationId,
                    Kind = item.Kind,
                    Title = item.Title,
                    Body = item.Body,
                    Reason = item.Reason,
                    Icon = item.Icon,
                    ActionLabel = item.ActionLabel,
                    ActionRoute = item.ActionRoute,
                    Confidence = item.Confidence,
                    ExpiresAt = item.ExpiresAt
                })
                .ToList());
    }
}

/// <summary>
/// Dismisses one creator suggestion for the caller, on every device.
/// <para>
/// <b>Idempotent, and any well-formed key is accepted.</b> A suggestion can be dismissed while the
/// list that showed it is being recomputed; refusing a key not served at this instant would turn a
/// tap into an error for nothing, and a repeat dismissal is the same dismissal.
/// </para>
/// </summary>
[RequirePermission(RecommendationPermissions.FeedbackRecord)]
[Auditable(Action = "recommendations.creator.dismiss")]
public sealed record DismissCreatorRecommendationCommand : ICommand<CreatorRecommendationDismissedResponse>
{
    public required string RecommendationId { get; init; }
}

public sealed class DismissCreatorRecommendationCommandValidator
    : AbstractValidator<DismissCreatorRecommendationCommand>
{
    public DismissCreatorRecommendationCommandValidator() =>
        RuleFor(command => command.RecommendationId)
            .NotEmpty()
            .MaximumLength(CreatorRecommendationLimits.MaxRecommendationIdLength);
}

public sealed class DismissCreatorRecommendationCommandHandler
    : ICommandHandler<DismissCreatorRecommendationCommand, CreatorRecommendationDismissedResponse>
{
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IEnumerable<ICreatorRecommendationProvider> _providers;
    private readonly IUnitOfWork _unitOfWork;

    public DismissCreatorRecommendationCommandHandler(
        RecommendationAuthorizationService authorization,
        IEnumerable<ICreatorRecommendationProvider> providers,
        IUnitOfWork unitOfWork)
    {
        _authorization = authorization;
        _providers = providers;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreatorRecommendationDismissedResponse>> Handle(
        DismissCreatorRecommendationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorRecommendationDismissedResponse>(caller.Error);
        }

        var provider = _providers.FirstOrDefault();

        if (provider is null)
        {
            return Result.Failure<CreatorRecommendationDismissedResponse>(Error.NotFound(
                RecommendationErrorCodes.RecommendationNotFound,
                "Creator suggestions are not served on this deployment."));
        }

        var key = command.RecommendationId.Trim();

        var dismissedAt = await provider.StageDismissalAsync(caller.Value.Value, key, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreatorRecommendationDismissedResponse
        {
            RecommendationId = key,
            DismissedAt = dismissedAt
        });
    }
}
