using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Extensibility.Caching;
using Diaspora.Modules.Recommendations.Infrastructure.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Policies;

/// <summary>
/// Applies a policy change, and clears the cache so it takes effect now.
/// <para>
/// <b>A refusal from the aggregate is translated into a forbidden result rather than allowed to
/// escape.</b> Disabling a system policy throws a domain exception — which is the right shape for a
/// rule that must hold on every path — and this handler turns it into the 403 the API contract
/// expects, with the aggregate's own message.
/// </para>
/// </summary>
public sealed class UpdateRecommendationPolicyCommandHandler
    : ICommandHandler<UpdateRecommendationPolicyCommand, RecommendationPolicyResponse>
{
    /// <summary>Matches the evaluator's cache key. Changing one means changing both.</summary>
    private const string PolicyCacheKey = "recommendations:policies:enabled";

    private readonly IRecommendationPolicyRepository _policies;
    private readonly RecommendationPolicyRuleRegistry _rules;
    private readonly IRecommendationCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRecommendationPolicyCommandHandler(
        IRecommendationPolicyRepository policies,
        RecommendationPolicyRuleRegistry rules,
        IRecommendationCache cache,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _policies = policies;
        _rules = rules;
        _cache = cache;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RecommendationPolicyResponse>> Handle(
        UpdateRecommendationPolicyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var policy = await _policies.FindAsync(
            RecommendationPolicyId.From(command.PolicyId), cancellationToken);

        if (policy is null)
        {
            return Result.Failure<RecommendationPolicyResponse>(Error.NotFound(
                RecommendationErrorCodes.PolicyNotFound,
                "That recommendation policy does not exist."));
        }

        try
        {
            Apply(command, policy);
        }
        catch (DomainException exception)
        {
            // A system policy refusing to be disabled. See the class summary.
            return Result.Failure<RecommendationPolicyResponse>(
                Error.Forbidden(exception.Code, exception.Message));
        }

        _policies.Update(policy);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, for the reason the strategy handler records: clearing in front of a change
        // that then failed to commit would re-cache the old row.
        await _cache.RemoveAsync(PolicyCacheKey, cancellationToken);

        return Result.Success(
            RecommendationMapper.ToResponse(policy, _rules.IsImplemented(policy.Key)));
    }

    private void Apply(UpdateRecommendationPolicyCommand command, RecommendationPolicy policy)
    {
        var now = _clock.UtcNow;

        if (command.Order is { } order) policy.SetOrder(order, now);
        if (command.DemotionFactor is { } factor) policy.SetDemotionFactor(factor, now);
        if (command.Description is not null) policy.Describe(command.Description, now);

        if (command.Surfaces is not null || command.TargetTypes is not null)
        {
            policy.SetScope(
                command.Surfaces?.Select(
                    surface => Enum.Parse<RecommendationSurface>(surface, ignoreCase: true)),
                command.TargetTypes?.Select(
                    type => Enum.Parse<RecommendationTargetType>(type, ignoreCase: true)),
                now);
        }

        foreach (var (key, value) in command.Parameters ?? new Dictionary<string, string>())
        {
            policy.SetParameter(key, value, _guids.NewSequentialGuid());
        }

        if (command.IsEnabled is { } enabled)
        {
            if (enabled)
            {
                policy.Enable(now);
            }
            else
            {
                policy.Disable(now);
            }
        }
    }
}
