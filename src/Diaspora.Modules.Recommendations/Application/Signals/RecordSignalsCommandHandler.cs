using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Signals;

/// <summary>
/// Records a client's batch of observations.
/// <para>
/// <b>The caller's identity comes from the token and nowhere else.</b> The request carries no user
/// identifier — see <c>RecordSignalRequest</c> — so a member cannot record signals against anybody
/// but themselves. It is the sort of hole that is obvious in hindsight and easy to leave open, and
/// the request shape is what closes it rather than a check that could be forgotten.
/// </para>
/// <para>
/// <b>Every draft is stamped <see cref="SignalSource.Client"/>, whatever it claims.</b> That is what
/// the weight ceiling keys on: a client can report attention and cannot report commitment, so a
/// modified app cannot inflate its own member's interests by claiming they transacted.
/// </para>
/// </summary>
public sealed class RecordSignalsCommandHandler
    : ICommandHandler<RecordSignalsCommand, SignalsRecordedResponse>
{
    private readonly ISignalStore _signals;
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;

    public RecordSignalsCommandHandler(
        ISignalStore signals,
        IPersonalizationPreferenceRepository preferences,
        RecommendationAuthorizationService authorization,
        IUnitOfWork unitOfWork)
    {
        _signals = signals;
        _preferences = preferences;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SignalsRecordedResponse>> Handle(
        RecordSignalsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<SignalsRecordedResponse>(caller.Error);
        }

        var drafts = command.Signals.Select(request => ToDraft(request, caller.Value)).ToList();
        var accepted = await _signals.RecordManyAsync(drafts, cancellationToken);

        // A handler saves its own work in this platform: the transaction behaviour opens a
        // transaction and does not flush. Omitting this is the defect that looks like the signal
        // store silently dropping everything.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var preference = await _preferences.FindAsync(caller.Value, cancellationToken);

        return Result.Success(new SignalsRecordedResponse
        {
            Accepted = accepted,
            Dropped = drafts.Count - accepted,

            // So a client can stop sending rather than retry against a preference that will not
            // change. See SignalsRecordedResponse.
            IsRecordingEnabled =
                preference?.IsPersonalizationEnabled != false &&
                preference?.ShareActivitySignals != false
        });
    }

    /// <summary>
    /// Turns a request into a draft, with the caller's identity and the client source.
    /// <para>
    /// Everything the validator has already checked is parsed without a fallback path — an
    /// unparseable value cannot reach here.
    /// </para>
    /// </summary>
    private static SignalDraft ToDraft(RecordSignalRequest request, SharedKernel.Identity.UserId userId) =>
        new()
        {
            UserId = userId,
            Kind = Enum.Parse<SignalKind>(request.Kind, ignoreCase: true),

            // Stamped, never taken from the request. See the class summary.
            Source = SignalSource.Client,
            TargetType = Parse<RecommendationTargetType>(request.TargetType),
            TargetId = request.TargetId,
            Key = request.Key,
            KeyKind = Parse<InterestKind>(request.KeyKind),
            Surface = Parse<RecommendationSurface>(request.Surface),
            Weight = request.Weight,
            Context = SignalContext.Create(
                request.CountryCode, request.CityName, request.NeighborhoodName),
            SourceEventName = "client",
            OccurredAt = request.OccurredAt
        };

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
