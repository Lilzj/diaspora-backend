using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Agreeing, or withdrawing agreement, with a published insight.
/// <para>
/// <b>The count is recomputed from the rows rather than incremented.</b> The unique index on
/// (insight, member) is what makes the number mean something, and reading the count back after the
/// row is written is the only way it can never disagree with the rows it summarises.
/// </para>
/// <para>
/// Withdrawing hard-deletes the row. A member saying they no longer agree should not leave a
/// tombstone still naming them beside an opinion they retracted.
/// </para>
/// </summary>
public sealed class EndorseNeighborhoodInsightCommandHandler
    : ICommandHandler<EndorseNeighborhoodInsightCommand, NeighborhoodInsightResponse>
{
    private readonly INeighborhoodInsightRepository _insights;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public EndorseNeighborhoodInsightCommandHandler(
        INeighborhoodInsightRepository insights,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _insights = insights;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodInsightResponse>> Handle(
        EndorseNeighborhoodInsightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(caller.Error);
        }

        var insightId = NeighborhoodInsightId.From(command.InsightId);
        var insight = await _insights.GetAsync(insightId, cancellationToken);

        if (insight is null || !insight.IsPublished)
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.InsightNotFound);
        }

        var already = await _insights.HasEndorsedAsync(
            insightId, caller.Value, cancellationToken);

        if (command.Request.Endorsed && !already)
        {
            await _insights.AddEndorsementAsync(
                NeighborhoodInsightEndorsement.Create(
                    _guids.NewSequentialGuid(), insightId, caller.Value, _clock.UtcNow),
                cancellationToken);
        }
        else if (!command.Request.Endorsed && already)
        {
            await _insights.RemoveEndorsementAsync(
                insightId, caller.Value, cancellationToken);
        }

        // Saved before the count is read back, so the tally reflects the row just written.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var total = await _insights.CountEndorsementsAsync(insightId, cancellationToken);

        var recorded = insight.Endorse(caller.Value, total, _clock.UtcNow);

        if (recorded.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(recorded.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(NeighborhoodMapper.ToResponse(
            insight,
            caller.Value,
            _authorization.IsStaff,
            command.Request.Endorsed));
    }
}
