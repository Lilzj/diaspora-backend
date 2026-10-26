using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Reports;

/// <summary>
/// Somebody saying a listing is not what it claims to be.
/// <para>
/// <b>Housing's report and Community's report are different things, deliberately.</b> A B6 report
/// is about content — an abusive question under a listing. This is about a property: the advert is
/// a scam, the flat is already let, the photographs are of somewhere else, the "landlord" does not
/// own it. They are triaged by different people against different evidence, and folding them into
/// one queue would bury the rental fraud under fifty rude comments.
/// </para>
/// <para>
/// <b><see cref="HousingReportReason.Scam"/> is treated differently everywhere it appears.</b> A
/// rental advance-fee fraud takes a newcomer's deposit before they have a bank account, a job, or
/// anyone to ask — and by the time they land, the advert is gone. One report of it flags the
/// listing immediately; every other reason waits for the configured threshold.
/// </para>
/// <para>
/// <b>The reporter's identity never leaves this row.</b> It is not on the domain event, not on the
/// published contract, and not on any response a landlord can reach. A member who reports a scam
/// must never be identifiable to the person they reported.
/// </para>
/// <para>
/// Never soft-deleted: a report is a record of a decision, and the trail of what was reported and
/// what was done about it is what an appeal and an audit are answered from.
/// </para>
/// </summary>
public sealed class HousingReport : AggregateRoot<HousingReportId>, IAuditableEntity
{
    private HousingReport(
        HousingReportId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        UserId reportedBy,
        HousingReportReason reason,
        string? detail,
        DateTimeOffset createdAt) : base(id)
    {
        ListingId = listingId;
        LandlordId = landlordId;
        ReportedBy = reportedBy;
        Reason = reason;
        Detail = detail;
        Status = HousingReportStatus.Open;
        CreatedAt = createdAt;
    }

    private HousingReport() { }

    public HousingListingId ListingId { get; private set; } = default!;

    /// <summary>
    /// Denormalised from the listing, so a moderator can ask "what else has this landlord had
    /// reported" without joining through every property they have ever listed.
    /// </summary>
    public LandlordProfileId LandlordId { get; private set; } = default!;

    /// <summary>Who reported it. Never published — see the class summary.</summary>
    public UserId ReportedBy { get; private set; } = default!;

    public HousingReportReason Reason { get; private set; }

    /// <summary>What they said. Free text, read by a moderator.</summary>
    public string? Detail { get; private set; }

    public HousingReportStatus Status { get; private set; }

    public UserId? ResolvedBy { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? Resolution { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether this report is still waiting on somebody.</summary>
    public bool IsOpen =>
        Status is HousingReportStatus.Open or HousingReportStatus.UnderReview;

    /// <summary>
    /// Whether this report alone should flag the listing.
    /// <para>
    /// True for a scam allegation and false for everything else. The threshold for the rest is a
    /// configured number the handler applies; this is the one case where waiting for a second
    /// reporter means letting a fraud run for another day.
    /// </para>
    /// </summary>
    public bool FlagsImmediately => Reason == HousingReportReason.Scam;

    public static Result<HousingReport> File(
        HousingReportId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        UserId reportedBy,
        HousingReportReason reason,
        string? detail,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(landlordId);
        ArgumentNullException.ThrowIfNull(reportedBy);

        var trimmed = detail?.Trim();

        if (trimmed is { Length: > HousingLimits.MaxReportDetail })
        {
            trimmed = trimmed[..HousingLimits.MaxReportDetail];
        }

        return Result.Success(new HousingReport(
            id,
            listingId,
            landlordId,
            reportedBy,
            reason,
            string.IsNullOrWhiteSpace(trimmed) ? null : trimmed,
            createdAt));
    }

    /// <summary>A moderator has picked it up. Distinct from open, so a queue can show who is on it.</summary>
    public Result TakeUp(UserId moderatorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        if (!IsOpen)
        {
            return Result.Failure(HousingErrors.ReportAlreadyResolved);
        }

        Status = HousingReportStatus.UnderReview;
        ModifiedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// Closes the report.
    /// <para>
    /// Upholding and dismissing are both closures and are recorded distinctly: a landlord with
    /// three upheld reports is a different proposition from one with three dismissed ones, and a
    /// queue that only recorded "closed" would lose exactly that distinction.
    /// </para>
    /// </summary>
    public Result Resolve(
        UserId moderatorId, bool upheld, string? resolution, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        if (!IsOpen)
        {
            return Result.Failure(HousingErrors.ReportAlreadyResolved);
        }

        var trimmed = resolution?.Trim();

        if (trimmed is { Length: > HousingLimits.MaxResolutionNote })
        {
            trimmed = trimmed[..HousingLimits.MaxResolutionNote];
        }

        Status = upheld ? HousingReportStatus.Upheld : HousingReportStatus.Dismissed;
        ResolvedBy = moderatorId;
        ResolvedAt = now;
        Resolution = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        ModifiedAt = now;

        if (upheld)
        {
            RaiseDomainEvent(new Events.HousingReportUpheldDomainEvent
            {
                ReportId = Id,
                ListingId = ListingId,
                LandlordId = LandlordId,
                ReportedBy = ReportedBy,
                UpheldAt = now
            });
        }

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
