using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.Modules.Recommendations.Observability;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Application.Feedback;

/// <summary>
/// Records an interaction and everything that follows from it.
/// <para>
/// <b>Three things happen in one transaction, and the ordering matters.</b> The feedback row is
/// written; a dismissal or an acceptance is also recorded as a signal so the profile learns from it;
/// and a dismissal's suppression window is computed from the options. Splitting them would mean a
/// member's dismissal being honoured on the page and not in their profile, which is the shape that
/// produces "I keep telling it no and it keeps learning yes".
/// </para>
/// <para>
/// <b>The strategy and the reason are recovered from the generating set, and this is what makes
/// strategy performance measurable at all.</b> Without them a dismissal says only that somebody
/// refused something; with them it says which strategy was wrong and what it claimed. When the
/// client does not send a set identifier the handler looks one up by target, which costs a query and
/// is worth it.
/// </para>
/// <para>
/// <b>A repeated view is not counted twice.</b> A client that retries a report would otherwise
/// inflate the denominator of every rate on the surface — and a denominator that inflates makes
/// every strategy look worse in proportion to how flaky the network was.
/// </para>
/// </summary>
public sealed class RecordFeedbackCommandHandler
    : ICommandHandler<RecordFeedbackCommand, FeedbackRecordedResponse>
{
    /// <summary>How close together two identical reports must be to be treated as one.</summary>
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(5);

    private readonly IRecommendationFeedbackRepository _feedback;
    private readonly IRecommendationSetRepository _sets;
    private readonly ISignalStore _signals;
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RecordFeedbackCommandHandler(
        IRecommendationFeedbackRepository feedback,
        IRecommendationSetRepository sets,
        ISignalStore signals,
        RecommendationAuthorizationService authorization,
        IOptions<RecommendationOptions> options,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _feedback = feedback;
        _sets = sets;
        _signals = signals;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FeedbackRecordedResponse>> Handle(
        RecordFeedbackCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<FeedbackRecordedResponse>(caller.Error);
        }

        var kind = Enum.Parse<RecommendationFeedbackKind>(command.Kind, ignoreCase: true);
        var targetType = Enum.Parse<RecommendationTargetType>(command.TargetType, ignoreCase: true);
        var surface = Enum.Parse<RecommendationSurface>(command.Surface, ignoreCase: true);

        var now = _clock.UtcNow;
        var occurredAt = command.OccurredAt ?? now;

        if (await IsDuplicateAsync(caller.Value, targetType, command.TargetId, kind, now, cancellationToken))
        {
            return Result.Success(new FeedbackRecordedResponse
            {
                FeedbackId = Guid.Empty,
                Kind = kind.ToString(),
                WasRecorded = false
            });
        }

        // Recovers the strategy and the reason. See the class summary.
        var set = await ResolveSetAsync(
            caller.Value, command.SetId, targetType, command.TargetId, cancellationToken);

        var item = set?.FindItem(targetType, command.TargetId);

        // Null on a dismissal means "never show this again"; null on anything else means the field
        // does not apply. The two are written as one expression because they produce the same value,
        // and the aggregate is what distinguishes them.
        DateTimeOffset? suppressedUntil = null;

        if (kind == RecommendationFeedbackKind.Dismissed &&
            _options.Value.DismissalLifetime is { } lifetime)
        {
            suppressedUntil = now.Add(lifetime);
        }

        var feedbackId = RecommendationFeedbackId.From(_guids.NewSequentialGuid());

        var feedback = RecommendationFeedback.Record(
            feedbackId,
            caller.Value,
            TargetReference.Create(targetType, command.TargetId),
            kind,
            surface,
            occurredAt,
            set?.Id,
            item?.PrimaryStrategyKey,
            item?.Explanation.ReasonCode,
            item?.Score,
            command.Position ?? item?.Position,
            suppressedUntil);

        _feedback.Add(feedback);

        await RecordSignalAsync(caller.Value, kind, targetType, command.TargetId, surface, occurredAt, cancellationToken);

        // The feedback row, the suppression and the signal, together. See the class summary.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        RecommendationTelemetry.FeedbackRecorded.Add(
            1,
            new KeyValuePair<string, object?>("kind", kind.ToString()),
            new KeyValuePair<string, object?>("surface", surface.ToString()));

        return Result.Success(new FeedbackRecordedResponse
        {
            FeedbackId = feedbackId.Value,
            Kind = kind.ToString(),
            SuppressedUntil = suppressedUntil,
            WasRecorded = true
        });
    }

    /// <summary>
    /// Whether an identical report arrived moments ago.
    /// <para>
    /// Only the passive kinds are de-duplicated. A member who dismisses something twice has said
    /// something twice, and refusing the second would make the control feel unresponsive; a client
    /// that reports the same impression twice has said nothing new.
    /// </para>
    /// </summary>
    private async Task<bool> IsDuplicateAsync(
        UserId userId,
        RecommendationTargetType targetType,
        Guid targetId,
        RecommendationFeedbackKind kind,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (kind is not (RecommendationFeedbackKind.Viewed or RecommendationFeedbackKind.Clicked))
        {
            return false;
        }

        var latest = await _feedback.FindLatestAsync(
            userId, targetType, targetId, kind, cancellationToken);

        return latest is not null && now - latest.OccurredAt < DuplicateWindow;
    }

    /// <summary>
    /// The set that produced the card.
    /// <para>
    /// Prefers what the client sent, falls back to a lookup by target. The fallback is why a
    /// dismissal from an old app version still teaches the platform something.
    /// </para>
    /// </summary>
    private async Task<RecommendationSet?> ResolveSetAsync(
        UserId userId,
        Guid? setId,
        RecommendationTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        if (setId is { } id)
        {
            var supplied = await _sets.FindAsync(RecommendationSetId.From(id), cancellationToken);

            // A set that belongs to somebody else is not an error worth failing on — it is a client
            // bug or a stale cache — but it must not be used to attribute this member's feedback.
            if (supplied is not null && supplied.UserId == userId)
            {
                return supplied;
            }
        }

        return await _sets.FindMostRecentContainingAsync(
            userId, targetType, targetId, cancellationToken);
    }

    /// <summary>
    /// Turns an interaction into a signal, so the profile learns from it.
    /// <para>
    /// A view or a click is not recorded here: the client already records those through the signal
    /// endpoint, and recording them again would double-count every impression on the platform.
    /// </para>
    /// </summary>
    private Task RecordSignalAsync(
        UserId userId,
        RecommendationFeedbackKind kind,
        RecommendationTargetType targetType,
        Guid targetId,
        RecommendationSurface surface,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var signalKind = kind switch
        {
            RecommendationFeedbackKind.Accepted => SignalKind.RecommendationAccepted,
            RecommendationFeedbackKind.Dismissed => SignalKind.RecommendationDismissed,
            RecommendationFeedbackKind.Bookmarked => SignalKind.Saved,

            // Already recorded by the client. See the method summary.
            _ => (SignalKind?)null
        };

        if (signalKind is null)
        {
            return Task.CompletedTask;
        }

        return _signals.RecordAsync(
            new SignalDraft
            {
                UserId = userId,
                Kind = signalKind.Value,
                Source = SignalSource.Recommendations,
                TargetType = targetType,
                TargetId = targetId,
                Surface = surface,
                SourceEventName = "recommendations.feedback",
                OccurredAt = occurredAt
            },
            cancellationToken);
    }
}
