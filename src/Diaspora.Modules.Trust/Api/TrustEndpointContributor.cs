using Diaspora.Contracts.Moderation.V1;
﻿using Diaspora.Modules.Trust.Application.Appeals;
using Diaspora.Modules.Trust.Application.Eligibility;
using Diaspora.Modules.Trust.Application.Moderation;
using Diaspora.Modules.Trust.Application.Profile;
using Diaspora.Modules.Trust.Application.Reputation;
using Diaspora.Modules.Trust.Application.Verifications;
using Diaspora.Modules.Trust.Contracts.V1.Requests;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Trust.Api;

/// <summary>
/// The Trust module's HTTP surface, mapped as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs — validation,
/// authorisation, telemetry, transactions — and returns through <see cref="PlatformResults"/>
/// so the envelope and problem documents are byte-for-byte identical to Identity's
/// controllers. Nothing here is known to the host.
/// </para>
/// <para>
/// <b>Route ownership.</b> Everything hangs off <c>/api/v1/trust</c> except
/// <c>/api/v1/trust/decisions</c>, which belongs to Identity's <c>TrustDecisionsController</c>
/// and is the inbound channel this module <em>calls</em>. Mapping it here would collide with
/// the one contract ADR-002 depends on, so nothing below goes near it.
/// </para>
/// </summary>
public sealed class TrustEndpointContributor : IEndpointContributor
{
    public string Module => TrustModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(TrustModuleMetadata.RoutePrefix)
            .WithTags("Trust")
            // "diaspora-authenticated", not "diaspora-signin". This group used to draw on the
            // credential-exchange budget — ten requests a minute, per IP, shared with
            // /auth/login — which meant /trust/feature-requirements, the list every create
            // screen fetches when it opens, 429ed after a member looked at a few of them.
            // The literal rather than the host's constant: a module does not reference the host.
            .RequireRateLimiting("diaspora-authenticated");

        MapProfile(group);
        MapVerifications(group);
        MapAppeals(group);
        MapReputation(group);
        MapEligibility(group);
        MapModeration(group);
    }

    // ── Profile, timeline, badges ────────────────────────────────────────

    private static void MapProfile(RouteGroupBuilder group)
    {
        group.MapGet("/profile", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyTrustProfileQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetTrustProfile")
            .WithSummary("The caller's trust profile: level, health, verifications, badges and country standing.");

        group.MapGet("/profiles/{userId:guid}", async (
                Guid userId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetPublicTrustProfileQuery { UserId = userId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadPublic)
            .WithName("GetPublicTrustProfile")
            .WithSummary("Another member's public trust standing. No risk score, health reasons or violation counts.");

        group.MapGet("/timeline", async (
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetTrustTimelineQuery { Pagination = Paginate(page, pageSize) },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetTrustTimeline")
            .WithSummary("The caller's trust journey.");

        group.MapGet("/badges/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyBadgesQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetMyBadges")
            .WithSummary("The caller's active badges.");
    }

    // ── Verifications ────────────────────────────────────────────────────

    private static void MapVerifications(RouteGroupBuilder group)
    {
        // The three purpose-specific submission routes the frontend's wizards post to. Each
        // maps onto the same command; they exist separately because each screen has a
        // genuinely different payload, and a single polymorphic body would push the branching
        // into the client.
        group.MapPost("/verifications/diaspora", async (
                SubmitDiasporaVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var command = new SubmitVerificationCommand
                {
                    Type = VerificationType.Diaspora,
                    Nationality = request.Nationality,
                    ResidenceCountry = request.ResidenceCountry,
                    SupportingNotes = request.SupportingNotes,
                    SelfieReference = request.SelfieReference,
                    Documents =
                    [
                        new DocumentSubmission
                        {
                            DocumentType = ParseDocumentType(request.DocumentType),
                            StorageKey = request.DocumentReference,
                            DocumentNumber = request.DocumentNumber,
                            IssuingCountry = request.IssueCountry,
                            IssuedOn = request.IssueDate,
                            ExpiresOn = request.ExpiryDate
                        }
                    ]
                };

                var result = await sender.Send(command, cancellationToken);
                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(TrustPolicies.SubmitVerification)
            .WithName("SubmitDiasporaVerification")
            .WithSummary("Submit the diaspora eligibility verification.");

        group.MapPost("/verifications/identity", async (
                SubmitIdentityVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var command = new SubmitVerificationCommand
                {
                    Type = VerificationType.Identity,
                    SelfieReference = request.SelfieReference,
                    Documents =
                    [
                        new DocumentSubmission
                        {
                            DocumentType = ParseDocumentType(request.DocumentType),
                            StorageKey = request.DocumentReference,
                            DocumentNumber = request.DocumentNumber,
                            IssuingCountry = request.IssueCountry,
                            ExpiresOn = request.ExpiryDate
                        }
                    ]
                };

                var result = await sender.Send(command, cancellationToken);
                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(TrustPolicies.SubmitVerification)
            .WithName("SubmitIdentityVerification")
            .WithSummary("Submit government ID and a biometric selfie.");

        group.MapPost("/verifications/country", async (
                SubmitCountryVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var command = new SubmitVerificationCommand
                {
                    Type = VerificationType.Country,
                    TargetCountry = request.CountryCode,
                    ArrivalDate = request.ArrivalDate,
                    ExpertiseAreas = request.ExpertiseAreas,
                    SupportingNotes = request.SupportingNotes,
                    Documents =
                    [
                        new DocumentSubmission
                        {
                            DocumentType = ParseDocumentType(request.DocumentType),
                            StorageKey = request.DocumentReference
                        }
                    ]
                };

                var result = await sender.Send(command, cancellationToken);
                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(TrustPolicies.SubmitVerification)
            .WithName("SubmitCountryVerification")
            .WithSummary("Submit residency evidence for a country community.");

        // The generic route. A verification type added by a future IVerificationTypeHandler
        // is submittable through this without a new endpoint.
        group.MapPost("/verifications", async (
                Contracts.V1.Requests.SubmitVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var command = new SubmitVerificationCommand
                {
                    Type = ParseVerificationType(request.Type),
                    Nationality = request.Nationality,
                    ResidenceCountry = request.ResidenceCountry,
                    TargetCountry = request.TargetCountry,
                    ArrivalDate = request.ArrivalDate,
                    SupportingNotes = request.SupportingNotes,
                    SelfieReference = request.SelfieReference,
                    ExpertiseAreas = request.ExpertiseAreas,
                    Documents = (request.Documents ?? [])
                        .Select(document => new DocumentSubmission
                        {
                            DocumentType = ParseDocumentType(document.DocumentType),
                            StorageKey = document.DocumentReference,
                            DocumentNumber = document.DocumentNumber,
                            IssuingCountry = document.IssuingCountry,
                            IssuedOn = document.IssueDate,
                            ExpiresOn = document.ExpiryDate,
                            FileName = document.FileName,
                            ContentType = document.ContentType,
                            SizeInBytes = document.SizeInBytes
                        })
                        .ToList()
                };

                var result = await sender.Send(command, cancellationToken);
                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(TrustPolicies.SubmitVerification)
            .WithName("SubmitVerification")
            .WithSummary("Submit any supported verification type.");

        group.MapGet("/verifications", async (
                string? type,
                string? status,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListMyVerificationsQuery
                    {
                        Pagination = Paginate(page, pageSize),
                        Type = TryParse<VerificationType>(type),
                        Status = TryParse<VerificationStatus>(status)
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("ListMyVerifications")
            .WithSummary("The caller's verification requests.");

        group.MapGet("/verifications/{verificationId:guid}", async (
                Guid verificationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetVerificationQuery { VerificationId = verificationId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetVerification")
            .WithSummary("One verification with its documents and review history.");

        // ── Staff review ─────────────────────────────────────────────────

        group.MapGet("/verifications/queue", async (
                string? type,
                string? status,
                Guid? assignedTo,
                bool? overdueOnly,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListVerificationQueueQuery
                    {
                        Pagination = Paginate(page, pageSize),
                        Type = TryParse<VerificationType>(type),
                        Status = TryParse<VerificationStatus>(status),
                        AssignedTo = assignedTo,
                        OverdueOnly = overdueOnly ?? false
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReviewVerification)
            .WithName("ListVerificationQueue")
            .WithSummary("The staff verification review queue.");

        group.MapPost("/verifications/{verificationId:guid}/assign", async (
                Guid verificationId,
                bool? release,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new AssignVerificationCommand
                    {
                        VerificationId = verificationId,
                        Assign = release is not true
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReviewVerification)
            .WithName("AssignVerification")
            .WithSummary("Take a verification for review, or release it back to the queue.");

        group.MapPost("/verifications/{verificationId:guid}/review", async (
                Guid verificationId,
                ReviewVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ReviewVerificationCommand
                    {
                        VerificationId = verificationId,
                        Decision = ParseReviewDecision(request.Decision),
                        RejectionReason = TryParse<VerificationRejectionReason>(request.RejectionReason),
                        Details = request.Details,
                        Notes = request.Notes
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReviewVerification)
            .WithName("ReviewVerification")
            .WithSummary("Record a decision on a verification.");

        // The repair, not a second way to decide. See ReapplyVerificationOutcomeCommand: it
        // refuses anything that is not already Approved, so the self-review rule guarding the
        // decision is untouched.
        group.MapPost("/verifications/{verificationId:guid}/reapply-outcome", async (
                Guid verificationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ReapplyVerificationOutcomeCommand { VerificationId = verificationId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReviewVerification)
            .WithName("ReapplyVerificationOutcome")
            .WithSummary("Apply the standing effects of an approval that never granted anything.");
    }

    // ── Appeals ──────────────────────────────────────────────────────────

    private static void MapAppeals(RouteGroupBuilder group)
    {
        // One route replaces the frontend's two appeal endpoints. Its generic appeals form
        // and its diaspora-specific appeal both land here, distinguished by `type`.
        group.MapPost("/appeals", async (
                SubmitAppealRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SubmitAppealCommand
                    {
                        Type = ParseAppealType(request.Type),
                        TargetId = request.TargetId,
                        Reason = request.Reason,
                        EvidenceKeys = request.AdditionalDocuments,
                        IsCorrectionRequest = request.CorrectionRequest
                    },
                    cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(TrustPolicies.SubmitAppeal)
            .WithName("SubmitAppeal")
            .WithSummary("Appeal a trust or moderation decision.");

        group.MapGet("/appeals", async (
                string? status,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListMyAppealsQuery
                    {
                        Pagination = Paginate(page, pageSize),
                        Status = TryParse<AppealStatus>(status)
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("ListMyAppeals")
            .WithSummary("The caller's appeals.");

        group.MapGet("/appeals/{appealId:guid}", async (
                Guid appealId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetAppealQuery { AppealId = appealId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetAppeal")
            .WithSummary("One appeal with its review history.");

        group.MapGet("/appeals/queue", async (
                string? status,
                string? type,
                Guid? assignedTo,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListAppealQueueQuery
                    {
                        Pagination = Paginate(page, pageSize),
                        Status = TryParse<AppealStatus>(status),
                        Type = TryParse<AppealType>(type),
                        AssignedTo = assignedTo
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReviewAppeal)
            .WithName("ListAppealQueue")
            .WithSummary("The staff appeals queue.");

        group.MapPost("/appeals/{appealId:guid}/review", async (
                Guid appealId,
                ReviewAppealRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                // NG-42. This used to compare `Decision` to the literals "Approve" and
                // "Escalate" and let everything else fall through to `Upheld: false`. That
                // made a typo — "approved" with a d, a stray character, an empty string —
                // silently *decline* the member's appeal while returning 200 to the
                // reviewer, so neither side ever learned the decision had not been
                // understood. The module already states the rule that violated, in the doc
                // comment on ParseReviewDecision: a malformed decision string must never
                // approve or reject someone. An unrecognised decision is now a 400, which
                // is the only outcome that decides nothing. This cannot break the client:
                // `reviewAppeal` in the frontend's trust-api.ts asserts the same closed
                // set of three values before the request leaves the device.
                if (!TryParseAppealDecision(request.Decision, out var upheld, out var escalate))
                {
                    return PlatformResults.Ok(
                        context,
                        Result.Failure<AppealResponse>(TrustErrors.AppealDecisionUnrecognised));
                }

                var result = await sender.Send(
                    new ReviewAppealCommand
                    {
                        AppealId = appealId,
                        Upheld = upheld,
                        Escalate = escalate,
                        Explanation = request.Explanation
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReviewAppeal)
            .WithName("ReviewAppeal")
            .WithSummary("Decide or escalate an appeal.");
    }

    // ── Reputation ───────────────────────────────────────────────────────

    private static void MapReputation(RouteGroupBuilder group)
    {
        group.MapGet("/reputation/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyReputationQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetMyReputation")
            .WithSummary("The caller's reputation across contribution, verification and trust.");

        group.MapGet("/reputation/rules", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListReputationRulesQuery(), cancellationToken);

                // The same for every member and fixed for the life of a deployment. Private
                // because the route is authenticated; an hour because a rule change ships with a
                // deployment, and the app also holds it for the session.
                context.Response.Headers.CacheControl = "private, max-age=3600";

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("ListReputationRules")
            .WithSummary("What the platform pays reputation for: active rules that something actually scores.");

        group.MapGet("/reputation/me/events", async (
                string? dimension,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListMyReputationEventsQuery
                    {
                        Pagination = Paginate(page, pageSize),
                        Dimension = TryParse<ReputationDimension>(dimension)
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("ListMyReputationEvents")
            .WithSummary("The caller's scoring history.");

        group.MapPost("/contributions/confirmations", async (
                RecordConfirmationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RecordConfirmationCommand
                    {
                        TargetType = request.TargetType,
                        TargetId = request.TargetId,
                        AuthorUserId = request.AuthorUserId,
                        CountryCode = request.CountryCode,
                        Note = request.Note
                    },
                    cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(TrustPolicies.Confirm)
            .WithName("RecordConfirmation")
            .WithSummary("Confirm the accuracy of another member's contribution.")
            .WithDescription(
                "Records the confirmation and moves the target's tally. Deprecated as a scoring " +
                "path: pointsAwarded is always 0, because the target and its author are not " +
                "verified here. Confirmations earn the author reputation through a corroborating " +
                "reaction on the content instead.");

        group.MapGet("/contributions/confirmations", async (
                string targetType,
                string targetId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetConfirmationSummaryQuery
                    {
                        TargetType = targetType,
                        TargetId = targetId
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetConfirmationSummary")
            .WithSummary("How many members confirmed one thing, and whether the caller did.");
    }

    // ── Eligibility ──────────────────────────────────────────────────────

    private static void MapEligibility(RouteGroupBuilder group)
    {
        group.MapPost("/eligibility", async (
                EvaluateEligibilityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new EvaluateEligibilityCommand
                    {
                        Capability = request.Capability,
                        TargetCountry = request.TargetCountry,
                        DryRun = request.DryRun
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("EvaluateEligibility")
            .WithSummary("Whether the caller may take an action, with the reason if not.");

        group.MapGet("/feature-requirements", async (
                string? targetCountry,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetFeatureRequirementsQuery { TargetCountry = targetCountry },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetFeatureRequirements")
            .WithSummary("Unlock progress for every gated capability.");
    }

    // ── Moderation ───────────────────────────────────────────────────────

    private static void MapModeration(RouteGroupBuilder group)
    {
        group.MapGet("/moderation/queue", async (
                string? status,
                string? contentType,
                string? targetType,
                string? minimumPriority,
                Guid? assignedTo,
                bool? overdueOnly,
                bool? awaitingSecondReview,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListModerationQueueQuery
                    {
                        Pagination = Paginate(page, pageSize),
                        Status = TryParse<ModerationQueueStatus>(status),
                        ContentType = TryParse<ModerationContentType>(contentType),
                        TargetType = targetType,
                        MinimumPriority = TryParse<ModerationPriority>(minimumPriority),
                        AssignedTo = assignedTo,
                        OverdueOnly = overdueOnly ?? false,
                        AwaitingSecondReview = awaitingSecondReview ?? false
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.Moderate)
            .WithName("ListModerationQueue")
            .WithSummary("The moderation queue: listed cases, priority first.");

        group.MapGet("/moderation/cases/{itemId:guid}", async (
                Guid itemId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetModerationCaseQuery { ItemId = itemId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.Moderate)
            .WithName("GetModerationCase")
            .WithSummary("One case in full: its reports, without reporters, and every decision.");

        group.MapGet("/moderation/cases", async (
                string contentType,
                Guid contentId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetModerationCaseQuery { TargetType = contentType, ContentId = contentId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.Moderate)
            .WithName("FindModerationCase")
            .WithSummary("The latest case about a piece of content.");

        group.MapPost("/moderation/cases", async (
                OpenModerationCaseRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new OpenModerationCaseCommand
                    {
                        TargetType = request.ContentType,
                        ContentId = request.ContentId,
                        ReasonCode = ParseReasonCode(request.ReasonCode),
                        Note = request.Note
                    },
                    cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(TrustPolicies.Moderate)
            .WithName("OpenModerationCase")
            .WithSummary("Open a case with no report. A second member of staff checks it within 7 days.");

        group.MapPost("/moderation/queue/{itemId:guid}/assign", async (
                Guid itemId,
                bool? release,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new AssignModerationItemCommand
                    {
                        ItemId = itemId,
                        Assign = release is not true
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.Moderate)
            .WithName("AssignModerationItem")
            .WithSummary("Take a case, or release it.");

        group.MapPost("/moderation/queue/{itemId:guid}/decision", async (
                Guid itemId,
                RecordModerationDecisionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RecordModerationDecisionCommand
                    {
                        ItemId = itemId,
                        Outcome = ParseOutcome(request.Outcome),
                        ReasonCode = ParseReasonCode(request.ReasonCode),
                        Note = request.Note
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.Moderate)
            .WithName("RecordModerationDecision")
            .WithSummary("Decide a case: no action, hide pending review, remove (needs a second member of staff), or restore.");

        group.MapPost("/moderation/queue/{itemId:guid}/second-review", async (
                Guid itemId,
                ModerationSecondReviewRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CompleteSecondReviewCommand
                    {
                        ItemId = itemId,
                        Confirm = request.Confirm,
                        Instead = string.IsNullOrWhiteSpace(request.Instead) ? null : ParseOutcome(request.Instead),
                        Note = request.Note
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.Moderate)
            .WithName("CompleteModerationSecondReview")
            .WithSummary("A second member of staff confirms or overrules a removal, an emergency hide, or a staff-opened case.");

        group.MapGet("/moderation/cases/{itemId:guid}/mine", async (
                Guid itemId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyModerationCaseQuery { ItemId = itemId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("GetMyModerationCase")
            .WithSummary("A decision about the caller's own content: what happened, why, and what they can do.");

        group.MapPost("/moderation/cases/{itemId:guid}/review-request", async (
                Guid itemId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RequestCaseReviewCommand { ItemId = itemId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(TrustPolicies.ReadSelf)
            .WithName("RequestModerationReview")
            .WithSummary("The owner asks for something of theirs that was hidden to be looked at again.");
    }

    // ── Parsing ──────────────────────────────────────────────────────────
    //
    // Enums arrive as strings so the wire contract stays readable and stable. An unparseable
    // value falls back to a safe default rather than throwing: for a filter, null means "no
    // filter"; for a required discriminator, the fallback is the most restrictive option, so
    // a malformed request cannot widen what it is asking for. FluentValidation then rejects
    // anything genuinely invalid with a proper problem document.

    private static PaginationRequest Paginate(int? page, int? pageSize) =>
        new PaginationRequest
        {
            Page = page ?? PaginationRequest.DefaultPage,
            PageSize = pageSize ?? PaginationRequest.DefaultPageSize
        }.Sanitize();

    private static TEnum? TryParse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

    private static VerificationType ParseVerificationType(string value) =>
        TryParse<VerificationType>(value) ?? VerificationType.Diaspora;

    private static VerificationDocumentType ParseDocumentType(string value) =>
        TryParse<VerificationDocumentType>(value) ?? VerificationDocumentType.Other;

    private static AppealType ParseAppealType(string value) =>
        TryParse<AppealType>(value) ?? AppealType.Verification;

    /// <summary>
    /// Parses the three decisions <see cref="ReviewAppealRequest.Decision"/> documents —
    /// Approve, Reject, Escalate — into the two flags <see cref="ReviewAppealCommand"/>
    /// carries, and refuses anything else.
    /// <para>
    /// There is deliberately no fallback here, unlike the sibling parsers below. Those can
    /// default safely because a wrong guess is reversible: an unrecognised verification type
    /// becomes Diaspora, an unrecognised moderation action escalates to a human. An appeal
    /// review has no such outcome. Upholding on a bad string would hand out an approval
    /// nobody authorised; declining on one — which is what this code did before NG-42 —
    /// closes the member's last route of recourse. Escalating would be defensible but
    /// invents a decision the reviewer did not make, and leaves the typo in place for the
    /// next reviewer to inherit. Refusing the request is the only option that decides
    /// nothing and tells the caller why.
    /// </para>
    /// </summary>
    private static bool TryParseAppealDecision(string? value, out bool upheld, out bool escalate)
    {
        upheld = false;
        escalate = false;

        var decision = value?.Trim();

        if (string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase))
        {
            upheld = true;
            return true;
        }

        if (string.Equals(decision, "Escalate", StringComparison.OrdinalIgnoreCase))
        {
            escalate = true;
            return true;
        }

        // Reject is the one decision whose flags are both false, which is exactly why it
        // has to be matched explicitly rather than left as the fallthrough.
        return string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Defaults to <see cref="ReviewDecision.NeedMoreInfo"/> — the only reversible outcome.
    /// A malformed decision string must never approve or reject someone.
    /// </summary>
    private static ReviewDecision ParseReviewDecision(string value) =>
        TryParse<ReviewDecision>(value) ?? ReviewDecision.NeedMoreInfo;

    /// <summary>
    /// Defaults to <see cref="ModerationActionType.Escalate"/> for the same reason: an
    /// unrecognised action sends the item to a human rather than removing content.
    /// </summary>
    // An outcome or a reason code that does not parse becomes a value outside the enum, which the
    // validator refuses: a malformed decision must fail, never fall back to some default action.
    private static ModerationOutcome ParseOutcome(string? value) =>
        TryParse<ModerationOutcome>(value) is { } outcome && Enum.IsDefined(outcome)
            ? outcome
            : (ModerationOutcome)(-1);

    private static ModerationReasonCode ParseReasonCode(string? value) =>
        ModerationReasons.TryParse(value, out var code) ? code : (ModerationReasonCode)(-1);
}
