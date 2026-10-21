using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Contracts.Location.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.EventHandlers;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Presence;
using Diaspora.Modules.Location.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Presence;

/// <summary>
/// One foreground location report: resolve it to an area, notice an arrival, forget the point —
/// 21 Oct 2026, the server half of <c>location.enteredArea</c>.
/// <para>
/// <b>What is kept.</b> The most specific catalogue area the point is in, replacing the previous
/// one, and when it was evaluated. Not the point, not the accuracy, not a list of past areas.
/// </para>
/// <para>
/// <b>What counts as entering.</b> Every area the member is now inside and was not inside at the
/// last report — the neighbourhood and, when it is new too, the town and district around it. The
/// first report after switching on is a baseline, never an arrival. Moving between two
/// neighbourhoods of one town enters the second neighbourhood and not the town.
/// </para>
/// <para>
/// <b>What is published.</b> An entry only when it concerns one of the member's own active
/// <c>location.enteredArea</c> automations, asked of Automation through
/// <see cref="IAutomationAreaWatchDirectory"/>. Nobody else's rule can cause a fact about this
/// member's movements to leave the module, and an entry nobody watches leaves nothing on the bus.
/// </para>
/// <para>
/// Google-free: the area comes from the platform's own catalogue through <see cref="IAreaLocator"/>,
/// so a report costs no paid API call.
/// </para>
/// </summary>
public sealed class ReportPresenceCommandHandler
    : ICommandHandler<ReportPresenceCommand, PresencePingResponse>
{
    private readonly IMemberPresenceRepository _presence;
    private readonly IAreaLocator _locator;
    private readonly PresenceAreas _areas;
    private readonly IReadOnlyList<IAutomationAreaWatchDirectory> _watches;
    private readonly LocationIntegrationEventRelay _relay;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReportPresenceCommandHandler(
        IMemberPresenceRepository presence,
        IAreaLocator locator,
        PresenceAreas areas,
        IEnumerable<IAutomationAreaWatchDirectory> watches,
        LocationIntegrationEventRelay relay,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _presence = presence;
        _locator = locator;
        _areas = areas;
        _watches = [.. watches];
        _relay = relay;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PresencePingResponse>> Handle(
        ReportPresenceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentUser.UserId is not { } userId)
        {
            return Result.Failure<PresencePingResponse>(LocationErrors.NotAuthenticated);
        }

        if (!IsValid(request))
        {
            return Result.Failure<PresencePingResponse>(LocationErrors.PresenceCoordinatesInvalid);
        }

        var presence = await _presence.FindAsync(userId, cancellationToken);

        if (presence is not { IsEnabled: true })
        {
            return Result.Failure<PresencePingResponse>(LocationErrors.PresenceNotEnabled);
        }

        var now = _clock.UtcNow;

        if (presence.LastEvaluatedAt is { } last && now - last < PresenceAreas.MinimumInterval)
        {
            return Result.Success(NotEvaluated("tooSoon", last + PresenceAreas.MinimumInterval));
        }

        if (request.AccuracyMetres is > PresenceAreas.MaximumAccuracyMetres)
        {
            return Result.Success(NotEvaluated("tooCoarse", null));
        }

        var placement = await _locator.LocateAsync(
            new AreaFilingRequest { Latitude = request.Latitude, Longitude = request.Longitude },
            cancellationToken);

        var (area, chain) = await _areas.ChainAsync(placement?.AreaId, cancellationToken);
        var wasBaseline = presence.IsBaseline;
        var (_, previous) = await _areas.ChainAsync(presence.LastAreaId, cancellationToken);

        presence.Record(area?.Id.Value, now);

        string outcome;
        var announced = false;

        if (area is null)
        {
            outcome = "noArea";
        }
        else if (wasBaseline)
        {
            outcome = "baseline";
        }
        else
        {
            var entered = chain.Where(id => !previous.Contains(id)).ToList();

            if (entered.Count == 0)
            {
                outcome = "unchanged";
            }
            else
            {
                outcome = "entered";

                if (await ConcernsAsync(userId.Value, entered, cancellationToken))
                {
                    await _relay.PublishAsync(
                        new MemberEnteredAreaIntegrationEvent
                        {
                            UserId = userId.Value,
                            AreaId = area.Id.Value,
                            AreaName = area.Name.Value,
                            AreaLevel = area.Level.ToString(),
                            CountryCode = area.CountryCode.Value,
                            EnteredAreaIds = entered,
                            EnteredAt = now
                        },
                        cancellationToken);

                    announced = true;
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new PresencePingResponse
        {
            Evaluated = true,
            Outcome = outcome,
            Area = PresenceAreas.Describe(area),
            Announced = announced
        });
    }

    private async Task<bool> ConcernsAsync(
        Guid memberId, IReadOnlyList<Guid> entered, CancellationToken cancellationToken)
    {
        foreach (var directory in _watches)
        {
            var watch = await directory.GetAsync(memberId, cancellationToken);

            if (watch.Concerns(entered))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValid(ReportPresenceCommand request) =>
        double.IsFinite(request.Latitude) && request.Latitude is >= -90d and <= 90d
        && double.IsFinite(request.Longitude) && request.Longitude is >= -180d and <= 180d
        && (request.AccuracyMetres is not { } accuracy
            || (double.IsFinite(accuracy) && accuracy >= 0d));

    private static PresencePingResponse NotEvaluated(string outcome, DateTimeOffset? next) => new()
    {
        Evaluated = false,
        Outcome = outcome,
        Announced = false,
        NextAcceptedAt = next
    };
}
