using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Reports;

/// <summary>
/// A member's allegation about a listing.
/// <para>
/// <b>Marketplace owns this and Community does not, and the line is worth stating because B6 owns
/// reporting elsewhere.</b> Community's reports are about <em>content</em> — a comment, a post, a
/// discussion under a listing — and they go through B6's moderation pipeline unchanged. This is
/// about a <em>trade</em>: the item is counterfeit, the price is a bait, the seller took the money.
/// Those are triaged by different people against different evidence, and folding them into one
/// queue would bury the fraud report under fifty rude comments.
/// </para>
/// <para>
/// <b>One report per member per listing.</b> Keyed that way in the database, so a member who
/// reports the same listing twice updates their own report rather than inflating the count that
/// decides how urgently a moderator looks.
/// </para>
/// <para>
/// <b>Filing a report never takes a listing down.</b> It flags it into the queue; a module where
/// one report hid a listing would be one where a competitor can clear the category.
/// </para>
/// </summary>
public sealed class ListingReport : AggregateRoot<ListingReportId>, IAuditableEntity
{
    private ListingReport(
        ListingReportId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId reporterId,
        ListingReportReason reason,
        string? detail,
        DateTimeOffset createdAt)
        : base(id)
    {
        ListingId = listingId;
        SellerId = sellerId;
        ReporterId = reporterId;
        Reason = reason;
        Detail = detail;
        Status = ListingReportStatus.Open;
        CreatedAt = createdAt;
    }

    private ListingReport() { }

    public MarketplaceListingId ListingId { get; private set; } = default!;

    /// <summary>Denormalised so the queue can group by seller without a join per row.</summary>
    public SellerProfileId SellerId { get; private set; } = default!;

    public UserId ReporterId { get; private set; } = default!;

    public ListingReportReason Reason { get; private set; }

    /// <summary>The reporter's own words. The part a moderator actually reads.</summary>
    public string? Detail { get; private set; }

    public ListingReportStatus Status { get; private set; }

    public UserId? ReviewerId { get; private set; }

    /// <summary>What the moderator decided and why. Never shown to the reporter verbatim.</summary>
    public string? Resolution { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsOpen => Status is ListingReportStatus.Open or ListingReportStatus.Reviewing;

    public static Result<ListingReport> File(
        ListingReportId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId reporterId,
        ListingReportReason reason,
        string? detail,
        int totalReports,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(sellerId);
        ArgumentNullException.ThrowIfNull(reporterId);

        if (!Enum.IsDefined(reason))
        {
            return Result.Failure<ListingReport>(MarketplaceErrors.ReportReasonInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxReportNote })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxReportNote];
        }

        // "Other" with nothing said is a report nobody can act on, and accepting it wastes a
        // moderator's time and the reporter's.
        if (reason == ListingReportReason.Other && trimmed is null)
        {
            return Result.Failure<ListingReport>(MarketplaceErrors.ReportReasonInvalid);
        }

        var report = new ListingReport(
            id, listingId, sellerId, reporterId, reason, trimmed, now);

        report.RaiseDomainEvent(new ListingReportedDomainEvent
        {
            ReportId = id,
            ListingId = listingId,
            SellerId = sellerId,
            ReporterId = reporterId,
            Reason = reason,
            TotalReports = totalReports,
            ReportedAt = now
        });

        return Result.Success(report);
    }

    /// <summary>A moderator picks it up. Idempotent, so two moderators do not fight over it.</summary>
    public Result StartReview(UserId reviewerId)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);

        if (!IsOpen)
        {
            return Result.Failure(MarketplaceErrors.ReportAlreadyResolved);
        }

        Status = ListingReportStatus.Reviewing;
        ReviewerId = reviewerId;

        return Result.Success();
    }

    /// <summary>Closes the report, upheld or not.</summary>
    public Result Resolve(
        ListingReportStatus outcome, UserId reviewerId, string? resolution, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);

        if (outcome is not (ListingReportStatus.Actioned or ListingReportStatus.Dismissed))
        {
            return Result.Failure(MarketplaceErrors.ReportReasonInvalid);
        }

        if (!IsOpen)
        {
            return Result.Failure(MarketplaceErrors.ReportAlreadyResolved);
        }

        var trimmed = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxReportNote })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxReportNote];
        }

        Status = outcome;
        ReviewerId = reviewerId;
        Resolution = trimmed;
        ResolvedAt = now;

        RaiseDomainEvent(new ListingReportResolvedDomainEvent
        {
            ReportId = Id,
            ListingId = ListingId,
            Status = outcome,
            ResolvedBy = reviewerId,
            Resolution = trimmed,
            ResolvedAt = now,
            ReporterId = ReporterId,
            SellerId = SellerId
        });

        return Result.Success();
    }

    /// <summary>The reporter adds to what they said. Only while the report is still open.</summary>
    public Result AmendDetail(ListingReportReason reason, string? detail)
    {
        if (!IsOpen)
        {
            return Result.Failure(MarketplaceErrors.ReportAlreadyResolved);
        }

        if (!Enum.IsDefined(reason))
        {
            return Result.Failure(MarketplaceErrors.ReportReasonInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxReportNote })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxReportNote];
        }

        Reason = reason;
        Detail = trimmed;

        return Result.Success();
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
