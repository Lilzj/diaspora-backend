using Diaspora.Application.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Eligibility;

/// <summary>
/// Asks whether the caller may take an action.
/// <para>
/// A command rather than a query, despite reading like one: a non-dry-run evaluation consumes
/// a rate-limit permit, which is a side effect. Modelling it as a query would put a mutation
/// behind a GET and eventually behind a cache.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.ReadSelf)]
public sealed record EvaluateEligibilityCommand : ICommand<EligibilityDecisionResponse>
{
    public required string Capability { get; init; }

    public string? TargetCountry { get; init; }

    /// <summary>True inspects without consuming a rate-limit permit.</summary>
    public bool DryRun { get; init; } = true;
}

public sealed class EvaluateEligibilityCommandValidator : AbstractValidator<EvaluateEligibilityCommand>
{
    public EvaluateEligibilityCommandValidator()
    {
        RuleFor(command => command.Capability)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(command => command.TargetCountry)
            .Length(2).When(command => !string.IsNullOrWhiteSpace(command.TargetCountry));
    }
}

public sealed class EvaluateEligibilityCommandHandler
    : ICommandHandler<EvaluateEligibilityCommand, EligibilityDecisionResponse>
{
    private readonly EligibilityEvaluator _evaluator;
    private readonly ICurrentUser _currentUser;

    public EvaluateEligibilityCommandHandler(
        EligibilityEvaluator evaluator, ICurrentUser currentUser)
    {
        _evaluator = evaluator;
        _currentUser = currentUser;
    }

    public async Task<Result<EligibilityDecisionResponse>> Handle(
        EvaluateEligibilityCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<EligibilityDecisionResponse>(TrustErrors.NotAuthenticated);
        }

        var decision = await _evaluator.EvaluateAsync(
            userId, request.Capability, request.TargetCountry, request.DryRun, cancellationToken);

        TrustTelemetrySource.EligibilityChecks.Add(
            1,
            new KeyValuePair<string, object?>("capability", request.Capability),
            new KeyValuePair<string, object?>("code", decision.Code.ToString()));

        return Result.Success(decision.ToResponse());
    }
}

/// <summary>
/// The unlock checklist for every capability — the Feature Gates tab.
/// <para>
/// A query, because describing requirements consumes nothing.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.ReadSelf)]
public sealed record GetFeatureRequirementsQuery : IQuery<IReadOnlyList<FeatureRequirementResponse>>
{
    /// <summary>
    /// Which country community to measure against. Country-scoped thresholds are meaningless
    /// without one — "150 points" is always 150 points *somewhere*.
    /// </summary>
    public string? TargetCountry { get; init; }
}

public sealed class GetFeatureRequirementsQueryHandler
    : IQueryHandler<GetFeatureRequirementsQuery, IReadOnlyList<FeatureRequirementResponse>>
{
    private readonly EligibilityEvaluator _evaluator;
    private readonly ICurrentUser _currentUser;

    public GetFeatureRequirementsQueryHandler(
        EligibilityEvaluator evaluator, ICurrentUser currentUser)
    {
        _evaluator = evaluator;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<FeatureRequirementResponse>>> Handle(
        GetFeatureRequirementsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<FeatureRequirementResponse>>(
                TrustErrors.NotAuthenticated);
        }

        var progress = await _evaluator.DescribeRequirementsAsync(
            userId, request.TargetCountry, cancellationToken);

        IReadOnlyList<FeatureRequirementResponse> response =
            progress.Select(item => item.ToResponse()).ToList();

        return Result.Success(response);
    }
}
