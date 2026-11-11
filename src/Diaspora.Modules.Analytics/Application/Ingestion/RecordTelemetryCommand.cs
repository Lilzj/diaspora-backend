using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Options;
using Diaspora.SharedKernel.Results;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Ingestion;

/// <summary>
/// Accepts a batch of client telemetry.
/// <para>
/// FRONTEND-PHASE15-GAP-029, restating CREATOR-GAP-016. The register's own summary of what was
/// missing: "one function's worth of behaviour: somewhere to send the batch". This is the somewhere.
/// </para>
///
/// <para><b>Everything about this handler treats its input as hostile, and it is the only write path
/// in the module.</b></para>
/// <list type="bullet">
///   <item>
///     <b>The caller is never trusted to say who they are.</b> The request carries no user
///     identifier, the client's allow-list has never had one, and the server attributes every row
///     to the authenticated principal.
///   </item>
///   <item>
///     <b>The event name is looked up, not accepted.</b> An unregistered name is refused with a code
///     that names the problem, per event, so a client release that invents an event learns about it
///     immediately rather than reporting into silence for a month.
///   </item>
///   <item>
///     <b>A server-produced event cannot be submitted.</b> Saves, shares, comments, reactions and
///     conversions are facts a module commits and publishes; a client posting one would be
///     incrementing a business metric, which §41 forbids outright.
///   </item>
///   <item>
///     <b>Properties are allow-listed again here.</b> The client sanitises at emit; an endpoint that
///     accepted arbitrary keys would make that advisory, and the whole privacy design of four
///     phases of instrumentation rests on it.
///   </item>
///   <item>
///     <b>The batch is capped, the payload is bounded and the clock is clamped.</b> A write path
///     open to every device is a denial-of-service surface with a friendly name, and an unclamped
///     <c>occurredAt</c> lets a caller write into a period that has already been reported on.
///   </item>
/// </list>
///
/// <para>
/// <b>It returns 202 with a per-event verdict rather than a single status.</b> A batch answering
/// "accepted" would leave the client unable to tell a stored event from one the registry has never
/// heard of — the silent drop the registry exists to prevent, moved one layer up.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.TelemetryWrite)]
public sealed record RecordTelemetryCommand : ICommand<TelemetryIngestionResponse>
{
    public required TelemetryBatchRequest Request { get; init; }

    /// <summary>The request's correlation identifier, so a figure can be traced back to a call.</summary>
    public Guid? CorrelationId { get; init; }
}

public sealed class RecordTelemetryCommandValidator : AbstractValidator<RecordTelemetryCommand>
{
    public RecordTelemetryCommandValidator() =>
        RuleFor(command => command.Request).NotNull();
}

public sealed class RecordTelemetryCommandHandler
    : ICommandHandler<RecordTelemetryCommand, TelemetryIngestionResponse>
{
    /// <summary>
    /// The property keys a submitted event may carry.
    /// <para>
    /// <b>The client's own list, restated on the server, and deliberately not widened.</b> Every key
    /// here either names a <em>thing</em> or describes the shape of an interaction. What is absent
    /// and must stay absent: any key that names a person, any raw amount, any free text, any
    /// coordinate. Phase 8 made precise location structurally impossible on the client by leaving
    /// latitude and longitude off its allow-list — "an ingestion endpoint that accepted a coordinate
    /// would undo a phase of work", and this list is where that promise is kept.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        // What was acted on. Platform identifiers for content, never for people.
        "contentId",
        "contentType",
        // Low-cardinality categorisation.
        "category",
        "section",
        "kind",
        "source",
        "surface",
        "scope",
        "period",
        "state",
        "status",
        "result",
        "reason",
        "method",
        "contentDomain",
        "communityId",
        "countryCode",
        // Interaction descriptors.
        "position",
        "isSimulated",

        // ── Automation — B24 · FRONTEND-PHASE18-GAP-028 ───────────────────────────────────────
        //
        // Five keys, and the shortness of the list is the point. The client's automation vocabulary
        // registers twenty-four property keys; these are the ones the four client-submittable
        // automation events actually carry, and widening the server's list beyond what an accepted
        // event needs would weaken it for every event, not just for these.
        //
        // Every one of the five is either platform vocabulary or an integer. What stays absent is
        // what the client's own allow-list has refused since Phase 18 and for the same reasons: no
        // automation name, which members write freely and sometimes personally; no condition value —
        // the budget, the threshold, the country; no recipient count, which is a fact about a
        // community; no schedule time, because a set of them is a pattern of life.
        "triggerKey",
        "templateId",
        "gapId",
        "actionCount",
        "wasSimulated"
    };

    /// <summary>The most characters one property value may carry. The client caps at the same width.</summary>
    private const int MaximumValueLength = 64;

    private readonly AnalyticsIngestionService _ingestion;
    private readonly AnalyticsQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AnalyticsOptions _options;

    public RecordTelemetryCommandHandler(
        AnalyticsIngestionService ingestion,
        AnalyticsQueryContext context,
        IUnitOfWork unitOfWork,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _ingestion = ingestion;
        _context = context;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<TelemetryIngestionResponse>> Handle(
        RecordTelemetryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<TelemetryIngestionResponse>(caller.Error);
        }

        var submitted = command.Request.Events ?? [];

        if (submitted.Count == 0 || submitted.Count > _options.MaximumBatchSize)
        {
            return Result.Failure<TelemetryIngestionResponse>(
                AnalyticsErrors.BatchInvalid(_options.MaximumBatchSize));
        }

        var now = _context.Calendar.UtcNow;
        var facts = new List<AnalyticsFact?>(submitted.Count);
        var refusals = new string?[submitted.Count];

        for (var index = 0; index < submitted.Count; index++)
        {
            var (fact, refusal) = Translate(submitted[index], caller.Value, command.CorrelationId, now);

            facts.Add(fact);
            refusals[index] = refusal;
        }

        var accepted = facts.Where(fact => fact is not null).Select(fact => fact!).ToList();

        var outcomes = await _ingestion
            .IngestAsync(accepted, isClientSubmission: true, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<TelemetryEventResultResponse>(submitted.Count);
        var cursor = 0;

        for (var index = 0; index < submitted.Count; index++)
        {
            var name = submitted[index].Event ?? string.Empty;

            if (refusals[index] is { } refusal)
            {
                results.Add(new TelemetryEventResultResponse
                {
                    Index = index,
                    Event = name,
                    Status = "rejected",
                    Reason = refusal
                });

                continue;
            }

            var outcome = outcomes[cursor++];

            results.Add(new TelemetryEventResultResponse
            {
                Index = index,
                Event = name,
                Status = outcome.Outcome switch
                {
                    AnalyticsIngestionOutcome.Accepted => "accepted",
                    AnalyticsIngestionOutcome.Duplicate => "duplicate",
                    _ => "rejected"
                },
                Reason = outcome.Reason
            });
        }

        return Result.Success(new TelemetryIngestionResponse
        {
            Accepted = results.Count(result => result.Status == "accepted"),
            Duplicate = results.Count(result => result.Status == "duplicate"),
            Rejected = results.Count(result => result.Status == "rejected"),
            Results = results
        });
    }

    /// <summary>
    /// Turns one submitted event into a fact, or into a refusal.
    /// <para>
    /// <b>Refused here rather than in the ingestion service where the shape is wrong rather than the
    /// content.</b> A missing name, a disallowed property or an unparseable identifier is a
    /// malformed request; an unregistered event or a duplicate is a decision about a well-formed one.
    /// Keeping the two apart is what makes the per-event reason codes mean something to a client.
    /// </para>
    /// </summary>
    private static (AnalyticsFact? Fact, string? Refusal) Translate(
        TelemetryEventRequest request, Guid actor, Guid? correlationId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(request.Event))
        {
            return (null, AnalyticsErrorCodes.EventMalformed);
        }

        var properties = request.Properties ?? [];

        foreach (var key in properties.Keys)
        {
            if (!AllowedProperties.Contains(key))
            {
                return (null, AnalyticsErrorCodes.PropertyNotAllowed);
            }
        }

        foreach (var value in properties.Values)
        {
            if (value is { Length: > MaximumValueLength })
            {
                return (null, AnalyticsErrorCodes.EventMalformed);
            }
        }

        var contentType = AnalyticsContentType.Unknown;
        Guid? contentId = null;

        if (properties.TryGetValue("contentId", out var rawContentId)
            && !string.IsNullOrWhiteSpace(rawContentId))
        {
            if (!Guid.TryParse(rawContentId, out var parsed))
            {
                return (null, AnalyticsErrorCodes.EventMalformed);
            }

            contentId = parsed;
        }

        if (properties.TryGetValue("contentType", out var rawContentType))
        {
            contentType = AnalyticsContentTypes.FromRouteSegment(rawContentType);

            if (contentType == AnalyticsContentType.Unknown)
            {
                contentType = AnalyticsContentTypes.FromCommunity(rawContentType);
            }
        }

        Guid? communityId = null;

        if (properties.TryGetValue("communityId", out var rawCommunityId)
            && Guid.TryParse(rawCommunityId, out var parsedCommunity))
        {
            communityId = parsedCommunity;
        }

        // A profile view names a creator through the generic content identifier rather than a
        // `creatorId` property, because the client's allow-list bans keys that name a person and
        // permits generic content identifiers precisely so this case has somewhere to go.
        var subjectUserId = request.Event == AnalyticsEventTypes.ProfileViewed ? contentId : null;

        return (
            new AnalyticsFact(
                request.Event.Trim(),
                request.Version ?? 1,
                SourceModule: "client",
                request.OccurredAt ?? now,
                ActorUserId: actor,
                ContentType: contentType,
                ContentId: contentId,
                OwnerUserId: null,
                SubjectUserId: subjectUserId,
                CountryCode: Read(properties, "countryCode"),
                CommunityId: communityId,

                // B24. The automation vocabulary has no `surface` and no `kind`; it has a trigger
                // key and a gap or template id, which are the same two shapes under different names
                // — one says where in the product, the other says which flavour. Falling back rather
                // than adding two columns keeps the raw store's schema fixed and keeps every
                // vocabulary reporting into the same two low-cardinality slots, which is what makes
                // one dataset able to read all of them.
                Surface: Read(properties, "surface") ?? Read(properties, "triggerKey"),
                Qualifier: Read(properties, "kind")
                    ?? Read(properties, "gapId")
                    ?? Read(properties, "templateId"),
                Weight: 1,
                SourceEventId: null,
                CorrelationId: correlationId),
            null);
    }

    private static string? Read(Dictionary<string, string?> properties, string key) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
